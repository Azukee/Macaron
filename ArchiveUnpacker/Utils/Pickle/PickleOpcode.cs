namespace ArchiveUnpacker.Utils.Pickle
{
    public enum PickleOpcode
    {
        /// <summary>
        /// push special markobject on stack
        /// </summary>
        Mark            = '(',
        /// <summary>
        /// End of a pickle object
        /// </summary>
        Stop            = '.',
        /// <summary>
        /// Discard topmost stack item
        /// </summary>
        Pop             = '0',
        /// <summary>
        /// discard stack top through topmost markobject
        /// </summary>
        PopMark         = '1',
        Dup             = '2',
        Float           = 'F',
        Int             = 'I',
        Binint          = 'J',
        Binint1         = 'K',
        Long            = 'L',
        Binint2         = 'M',
        None            = 'N',
        /// <summary>
        /// push persistent object; id is taken from string arg
        /// </summary>
        Persid          = 'P',
        /// <summary>
        /// push persistent object; id is taken from stack
        /// </summary>
        Binpersid       = 'Q',
        Reduce          = 'R',
        /// <summary>
        /// push string; NL-terminated string argument
        /// </summary>
        String          = 'S',
        /// <summary>
        /// push string; counted binary string argument
        /// </summary>
        Binstring       = 'T',
        /// <summary>
        /// push string; counted binary string argument < 256
        /// </summary>
        ShortBinstring  = 'U',
        /// <summary>
        /// push Unicode string; raw-unicode-escaped'd argument
        /// </summary>
        Unicode         = 'V',
        /// <summary>
        /// push Unicode string; counted UTF-8 string argument
        /// </summary>
        Binunicode      = 'X',
        Append          = 'a',
        Build           = 'b',
        Global          = 'c',
        /// <summary>
        /// build a dict from stack items
        /// </summary>
        Dict            = 'd',
        /// <summary>
        /// push empty dict
        /// </summary>
        EmptyDict       = '}',
        Appends         = 'e',
        /// <summary>
        /// push item from memo on stack; index is string arg
        /// </summary>
        Get             = 'g',
        /// <summary>
        /// push item from memo on stack; index is 1-byte arch
        /// </summary>
        Binget          = 'h',
        Inst            = 'i',
        LongBinget      = 'j',
        List            = 'l',
        EmptyList       = ']',
        Obj             = 'o',
        /// <summary>
        /// store stack top in memo; index is string arg
        /// </summary>
        Put             = 'p',
        /// <summary>
        /// store stack top in memo; index is 1 byte arg
        /// </summary>
        Binput          = 'q',
        /// <summary>
        /// store stack top in memo; index is 4 byte arg
        /// </summary>
        LongBinput      = 'r',
        Setitem         = 's',
        Tuple           = 't',
        EmptyTuple      = ')',
        Setitems        = 'u',
        Binfloat        = 'G',

        /* Protocol 2. */
        /// <summary>
        /// identify pickle protocol
        /// </summary>
        Proto       = '\x80',
        Newobj      = '\x81',
        Ext1        = '\x82',
        Ext2        = '\x83',
        Ext4        = '\x84',
        Tuple1      = '\x85',
        Tuple2      = '\x86',
        Tuple3      = '\x87',
        Newtrue     = '\x88',
        Newfalse    = '\x89',
        Long1       = '\x8a',
        Long4       = '\x8b',

        /* Protocol 3 (Python 3.x) */
        Binbytes       = 'B',
        ShortBinbytes  = 'C',

        /* Protocol 4 */
        ShortBinunicode  = '\x8c',
        Binunicode8      = '\x8d',
        Binbytes8        = '\x8e',
        EmptySet         = '\x8f',
        Additems         = '\x90',
        Frozenset        = '\x91',
        NewobjEx         = '\x92',
        StackGlobal      = '\x93',
        Memoize          = '\x94',
        Frame            = '\x95'
    }
}
