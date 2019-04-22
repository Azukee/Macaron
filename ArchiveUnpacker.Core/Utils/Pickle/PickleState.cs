using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ArchiveUnpacker.Core.Utils.Pickle
{
    /// <remarks>
    /// For reference: https://github.com/python/cpython/blob/master/Lib/pickle.py
    /// </remarks>
    public class PickleState
    {
        private int proto = -1;

        private Stack<object> stack = new Stack<object>();
        private readonly Stack<Stack<object>> metaStack = new Stack<Stack<object>>();    // mark stack
        /// <summary>
        /// The memo is the data structure that remembers which objects the pickler has already seen, so that shared or recursive objects are pickled
        /// by reference and not by value.
        /// </summary>
        private readonly Dictionary<int, object> memo = new Dictionary<int, object>();

        public object ReadFromStream(Stream s)
        {
            using (var br = new BinaryReader(s, Encoding.UTF8, true)) {
                while (true) {
                    var c = (PickleOpcode)br.ReadByte();
                    switch (c) {
                        case PickleOpcode.Proto:
                            proto = br.ReadByte();
                            Debug.WriteLine("Version: " + proto);
                            break;
                        case PickleOpcode.EmptyDict: stack.Push(new Dictionary<object, object>()); break;
                        case PickleOpcode.EmptyList: stack.Push(new List<object>()); break;
                        case PickleOpcode.Binput: {
                            int i = br.ReadByte();
                            var value = stack.Peek();
                            memo[i] = value;
                            break;
                        }
                        case PickleOpcode.LongBinput: {
                            int i = br.ReadInt32();
                            var value = stack.Peek();
                            memo[i] = value;
                            break;
                        }
                        case PickleOpcode.Mark:
                            // push to the mark stack
                            metaStack.Push(stack);
                            stack = new Stack<object>();
                            break;
                        case PickleOpcode.Binunicode:
                            stack.Push(Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32())));
                            break;
                        case PickleOpcode.Long1: {
                            var padding = new byte[8];
                            var data = br.ReadBytes(br.ReadByte());
                            Array.Copy(data, padding, data.Length);
                            stack.Push(BitConverter.ToInt64(padding, 0));
                            break;
                        }
                        case PickleOpcode.Binint: stack.Push(br.ReadInt32()); break;
                        case PickleOpcode.ShortBinstring: stack.Push(Encoding.ASCII.GetString(br.ReadBytes(br.ReadByte()))); break;
                        case PickleOpcode.Tuple3: {
                            object obj3 = stack.Pop();
                            object obj2 = stack.Pop();
                            object obj1 = stack.Pop();
                            stack.Push(new [] {obj1, obj2, obj3});
                            break;
                        }
                        case PickleOpcode.Append: {
                            var value = stack.Pop();
                            var list = stack.Peek();
                            if (list is List<object> l) {
                                l.Add(value);
                            } else throw new Exception("Expected list object on stack, found " + list.GetType());

                            break;
                        }
                        case PickleOpcode.Setitems: {
                            var items = popMark().Reverse().ToArray();
                            var dict = stack.Peek();
                            if (dict is Dictionary<object, object> d) {
                                for (int i = 0; i < items.Length; i += 2) d[items[i]] = items[i + 1];
                            } else throw new Exception("Expected dictionary object on stack, found " + dict.GetType());
                            break;
                        }
                        case PickleOpcode.Stop:
                            Debug.Assert(stack.Count == 1);
                            return stack.Pop();
                        default:
                            throw new NotImplementedException($"Pickle opcode {c} not yet implemented");
                    }
                }
            }

            Stack<object> popMark()
            {
                var items = stack;
                stack = metaStack.Pop();
                return items;
            }
        }
    }
}
