using System.Reflection;

// butchered resource strings, kept to add to and replace with localisation

namespace Nanook.GrindCore
{
    internal static partial class SR
    {
        private static string GetResourceString(string name, string text) => text;


        /// <summary>Enum value was out of legal range.</summary>
        internal static string @ArgumentOutOfRange_Enum => GetResourceString("ArgumentOutOfRange_Enum", @"Enum value was out of legal range.");
        /// <summary>Reading from the compression stream is not supported.</summary>
        internal static string @CannotReadFromDeflateStream => GetResourceString("CannotReadFromDeflateStream", @"Reading from the compression stream is not supported.");
        /// <summary>Writing to the compression stream is not supported.</summary>
        internal static string @CannotWriteToDeflateStream => GetResourceString("CannotWriteToDeflateStream", @"Writing to the compression stream is not supported.");
        /// <summary>Found invalid data while decoding.</summary>
        internal static string @GenericInvalidData => GetResourceString("GenericInvalidData", @"Found invalid data while decoding.");
        /// <summary>Only one asynchronous reader or writer is allowed time at one time.</summary>
        internal static string @InvalidBeginCall => GetResourceString("InvalidBeginCall", @"Only one asynchronous reader or writer is allowed time at one time.");
        /// <summary>Block length does not match with its complement.</summary>
        internal static string @InvalidBlockLength => GetResourceString("InvalidBlockLength", @"Block length does not match with its complement.");
        /// <summary>Failed to construct a huffman tree using the length array. The stream might be corrupted.</summary>
        internal static string @InvalidHuffmanData => GetResourceString("InvalidHuffmanData", @"Failed to construct a huffman tree using the length array. The stream might be corrupted.");
        /// <summary>This operation is not supported.</summary>
        internal static string @NotSupported => GetResourceString("NotSupported", @"This operation is not supported.");
        /// <summary>Stream does not support reading.</summary>
        internal static string @NotSupported_UnreadableStream => GetResourceString("NotSupported_UnreadableStream", @"Stream does not support reading.");
        /// <summary>Stream does not support writing.</summary>
        internal static string @NotSupported_UnwritableStream => GetResourceString("NotSupported_UnwritableStream", @"Stream does not support writing.");
        /// <summary>Unknown block type. Stream might be corrupted.</summary>
        internal static string @UnknownBlockType => GetResourceString("UnknownBlockType", @"Unknown block type. Stream might be corrupted.");
        /// <summary>Decoder is in some unknown state. This might be caused by corrupted data.</summary>
        internal static string @UnknownState => GetResourceString("UnknownState", @"Decoder is in some unknown state. This might be caused by corrupted data.");
        /// <summary>The underlying compression routine could not be loaded correctly.</summary>
        internal static string @ZLibErrorDLLLoadError => GetResourceString("ZLibErrorDLLLoadError", @"The underlying compression routine could not be loaded correctly.");
        /// <summary>The stream state of the underlying compression routine is inconsistent.</summary>
        internal static string @ZLibErrorInconsistentStream => GetResourceString("ZLibErrorInconsistentStream", @"The stream state of the underlying compression routine is inconsistent.");
        /// <summary>The underlying compression routine received incorrect initialization parameters.</summary>
        internal static string @ZLibErrorIncorrectInitParameters => GetResourceString("ZLibErrorIncorrectInitParameters", @"The underlying compression routine received incorrect initialization parameters.");
        /// <summary>The underlying compression routine could not reserve sufficient memory.</summary>
        internal static string @ZLibErrorNotEnoughMemory => GetResourceString("ZLibErrorNotEnoughMemory", @"The underlying compression routine could not reserve sufficient memory.");
        /// <summary>The version of the underlying compression routine does not match expected version.</summary>
        internal static string @ZLibErrorVersionMismatch => GetResourceString("ZLibErrorVersionMismatch", @"The version of the underlying compression routine does not match expected version.");
        /// <summary>The underlying compression routine returned an unexpected error code.</summary>
        internal static string @ZLibErrorUnexpected => GetResourceString("ZLibErrorUnexpected", @"The underlying compression routine returned an unexpected error code.");
        /// <summary>Central Directory corrupt.</summary>
        internal static string @CDCorrupt => GetResourceString("CDCorrupt", @"Central Directory corrupt.");
        /// <summary>Central Directory is invalid.</summary>
        internal static string @CentralDirectoryInvalid => GetResourceString("CentralDirectoryInvalid", @"Central Directory is invalid.");
        /// <summary>Cannot create entries on an archive opened in read mode.</summary>
        internal static string @CreateInReadMode => GetResourceString("CreateInReadMode", @"Cannot create entries on an archive opened in read mode.");
        /// <summary>Cannot use create mode on a non-writable stream.</summary>
        internal static string @CreateModeCapabilities => GetResourceString("CreateModeCapabilities", @"Cannot use create mode on a non-writable stream.");
        /// <summary>Entries cannot be created while previously created entries are still open.</summary>
        internal static string @CreateModeCreateEntryWhileOpen => GetResourceString("CreateModeCreateEntryWhileOpen", @"Entries cannot be created while previously created entries are still open.");
        /// <summary>Entries in create mode may only be written to once, and only one entry may be held open at a time.</summary>
        internal static string @CreateModeWriteOnceAndOneEntryAtATime => GetResourceString("CreateModeWriteOnceAndOneEntryAtATime", @"Entries in create mode may only be written to once, and only one entry may be held open at a time.");
        /// <summary>The DateTimeOffset specified cannot be converted into a Zip file timestamp.</summary>
        internal static string @DateTimeOutOfRange => GetResourceString("DateTimeOutOfRange", @"The DateTimeOffset specified cannot be converted into a Zip file timestamp.");
        /// <summary>Cannot modify deleted entry.</summary>
        internal static string @DeletedEntry => GetResourceString("DeletedEntry", @"Cannot modify deleted entry.");
        /// <summary>Delete can only be used when the archive is in Update mode.</summary>
        internal static string @DeleteOnlyInUpdate => GetResourceString("DeleteOnlyInUpdate", @"Delete can only be used when the archive is in Update mode.");
        /// <summary>Cannot delete an entry currently open for writing.</summary>
        internal static string @DeleteOpenEntry => GetResourceString("DeleteOpenEntry", @"Cannot delete an entry currently open for writing.");
        /// <summary>Cannot access entries in Create mode.</summary>
        internal static string @EntriesInCreateMode => GetResourceString("EntriesInCreateMode", @"Cannot access entries in Create mode.");
        /// <summary>The specified encoding is not supported for entry names and comments.</summary>
        internal static string @EntryNameAndCommentEncodingNotSupported => GetResourceString("EntryNameAndCommentEncodingNotSupported", @"The specified encoding is not supported for entry names and comments.");
        /// <summary>Entry names cannot require more than 2^16 bits.</summary>
        internal static string @EntryNamesTooLong => GetResourceString("EntryNamesTooLong", @"Entry names cannot require more than 2^16 bits.");
        /// <summary>Entries larger than 4GB are not supported in Update mode.</summary>
        internal static string @EntryTooLarge => GetResourceString("EntryTooLarge", @"Entries larger than 4GB are not supported in Update mode.");
        /// <summary>End of Central Directory record could not be found.</summary>
        internal static string @EOCDNotFound => GetResourceString("EOCDNotFound", @"End of Central Directory record could not be found.");
        /// <summary>Compressed Size cannot be held in an Int64.</summary>
        internal static string @FieldTooBigCompressedSize => GetResourceString("FieldTooBigCompressedSize", @"Compressed Size cannot be held in an Int64.");
        /// <summary>Local Header Offset cannot be held in an Int64.</summary>
        internal static string @FieldTooBigLocalHeaderOffset => GetResourceString("FieldTooBigLocalHeaderOffset", @"Local Header Offset cannot be held in an Int64.");
        /// <summary>Number of Entries cannot be held in an Int64.</summary>
        internal static string @FieldTooBigNumEntries => GetResourceString("FieldTooBigNumEntries", @"Number of Entries cannot be held in an Int64.");
        /// <summary>Offset to Central Directory cannot be held in an Int64.</summary>
        internal static string @FieldTooBigOffsetToCD => GetResourceString("FieldTooBigOffsetToCD", @"Offset to Central Directory cannot be held in an Int64.");
        /// <summary>Offset to Zip64 End Of Central Directory record cannot be held in an Int64.</summary>
        internal static string @FieldTooBigOffsetToZip64EOCD => GetResourceString("FieldTooBigOffsetToZip64EOCD", @"Offset to Zip64 End Of Central Directory record cannot be held in an Int64.");
        /// <summary>Uncompressed Size cannot be held in an Int64.</summary>
        internal static string @FieldTooBigUncompressedSize => GetResourceString("FieldTooBigUncompressedSize", @"Uncompressed Size cannot be held in an Int64.");
        /// <summary>Cannot modify entry in Create mode after entry has been opened for writing.</summary>
        internal static string @FrozenAfterWrite => GetResourceString("FrozenAfterWrite", @"Cannot modify entry in Create mode after entry has been opened for writing.");
        /// <summary>A stream from ZipArchiveEntry has been _disposed.</summary>
        internal static string @HiddenStreamName => GetResourceString("HiddenStreamName", @"A stream from ZipArchiveEntry has been disposed.");
        /// <summary>Length properties are unavailable once an entry has been opened for writing.</summary>
        internal static string @LengthAfterWrite => GetResourceString("LengthAfterWrite", @"Length properties are unavailable once an entry has been opened for writing.");
        /// <summary>A local file header is corrupt.</summary>
        internal static string @LocalFileHeaderCorrupt => GetResourceString("LocalFileHeaderCorrupt", @"A local file header is corrupt.");
        /// <summary>Number of entries expected in End Of Central Directory does not correspond to number of entries in Central Directory.</summary>
        internal static string @NumEntriesWrong => GetResourceString("NumEntriesWrong", @"Number of entries expected in End Of Central Directory does not correspond to number of entries in Central Directory.");
        /// <summary>This stream from ZipArchiveEntry does not support reading.</summary>
        internal static string @ReadingNotSupported => GetResourceString("ReadingNotSupported", @"This stream from ZipArchiveEntry does not support reading.");
        /// <summary>Cannot use read mode on a non-readable stream.</summary>
        internal static string @ReadModeCapabilities => GetResourceString("ReadModeCapabilities", @"Cannot use read mode on a non-readable stream.");
        /// <summary>Cannot modify read-only archive.</summary>
        internal static string @ReadOnlyArchive => GetResourceString("ReadOnlyArchive", @"Cannot modify read-only archive.");
        /// <summary>This stream from ZipArchiveEntry does not support seeking.</summary>
        internal static string @SeekingNotSupported => GetResourceString("SeekingNotSupported", @"This stream from ZipArchiveEntry does not support seeking.");
        /// <summary>SetLength requires a stream that supports seeking and writing.</summary>
        internal static string @SetLengthRequiresSeekingAndWriting => GetResourceString("SetLengthRequiresSeekingAndWriting", @"SetLength requires a stream that supports seeking and writing.");
        /// <summary>Split or spanned archives are not supported.</summary>
        internal static string @SplitSpanned => GetResourceString("SplitSpanned", @"Split or spanned archives are not supported.");
        /// <summary>Found truncated data while decoding.</summary>
        internal static string @TruncatedData => GetResourceString("TruncatedData", @"Found truncated data while decoding.");
        /// <summary>Zip file corrupt: unexpected end of stream reached.</summary>
        internal static string @UnexpectedEndOfStream => GetResourceString("UnexpectedEndOfStream", @"Zip file corrupt: unexpected end of stream reached.");
        /// <summary>The archive entry was compressed using an unsupported compression method.</summary>
        internal static string @UnsupportedCompression => GetResourceString("UnsupportedCompression", @"The archive entry was compressed using an unsupported compression method.");
        /// <summary>The archive entry was compressed using {0} and is not supported.</summary>
        internal static string @UnsupportedCompressionMethod => GetResourceString("UnsupportedCompressionMethod", @"The archive entry was compressed using {0} and is not supported.");
        /// <summary>Update mode requires a stream with read, write, and seek capabilities.</summary>
        internal static string @UpdateModeCapabilities => GetResourceString("UpdateModeCapabilities", @"Update mode requires a stream with read, write, and seek capabilities.");
        /// <summary>Entries cannot be opened multiple times in Update mode.</summary>
        internal static string @UpdateModeOneStream => GetResourceString("UpdateModeOneStream", @"Entries cannot be opened multiple times in Update mode.");
        /// <summary>This stream from ZipArchiveEntry does not support writing.</summary>
        internal static string @WritingNotSupported => GetResourceString("WritingNotSupported", @"This stream from ZipArchiveEntry does not support writing.");
        /// <summary>Zip 64 End of Central Directory Record not where indicated.</summary>
        internal static string @Zip64EOCDNotWhereExpected => GetResourceString("Zip64EOCDNotWhereExpected", @"Zip 64 End of Central Directory Record not where indicated.");
        /// <summary>Nanook.GrindCore is not supported on this platform.</summary>
        internal static string @PlatformNotSupported_Compression => GetResourceString("PlatformNotSupported_Compression", @"Nanook.GrindCore is not supported on this platform.");

        /// <summary>Stream does not support reading.</summary>
        internal static string @Stream_FalseCanRead => GetResourceString("Stream_FalseCanRead", @"Stream does not support reading.");
        /// <summary>Stream does not support writing.</summary>
        internal static string @Stream_FalseCanWrite => GetResourceString("Stream_FalseCanWrite", @"Stream does not support writing.");
        /// <summary>Positive number required.</summary>
        internal static string @ArgumentOutOfRange_NeedPosNum => GetResourceString("ArgumentOutOfRange_NeedPosNum", @"Positive number required.");
        /// <summary>Failed to create BrotliEncoder instance</summary>
        internal static string @BrotliEncoder_Create => GetResourceString("BrotliEncoder_Create", @"Failed to create BrotliEncoder instance");
        /// <summary>Can not access a closed Encoder.</summary>
        internal static string @BrotliEncoder_Disposed => GetResourceString("BrotliEncoder_Disposed", @"Can not access a closed Encoder.");
        /// <summary>Provided BrotliEncoder Quality of {0} is not between the minimum value of {1} and the maximum value of {2}</summary>
        internal static string @BrotliEncoder_Quality => GetResourceString("BrotliEncoder_Quality", @"Provided BrotliEncoder Quality of {0} is not between the minimum value of {1} and the maximum value of {2}");
        /// <summary>Provided BrotliEncoder Window of {0} is not between the minimum value of {1} and the maximum value of {2}</summary>
        internal static string @BrotliEncoder_Window => GetResourceString("BrotliEncoder_Window", @"Provided BrotliEncoder Window of {0} is not between the minimum value of {1} and the maximum value of {2}");
        /// <summary>The BrotliEncoder {0} can not be changed at current encoder state.</summary>
        internal static string @BrotliEncoder_InvalidSetParameter => GetResourceString("BrotliEncoder_InvalidSetParameter", @"The BrotliEncoder {0} can not be changed at current encoder state.");
        /// <summary>Failed to create BrotliDecoder instance</summary>
        internal static string @BrotliDecoder_Create => GetResourceString("BrotliDecoder_Create", @"Failed to create BrotliDecoder instance");
        /// <summary>Can not access a closed Decoder.</summary>
        internal static string @BrotliDecoder_Disposed => GetResourceString("BrotliDecoder_Disposed", @"Can not access a closed Decoder.");
        /// <summary>Can not perform Read operations on a BrotliStream constructed with CompressionMode.Process.</summary>
        internal static string @BrotliStream_Compress_UnsupportedOperation => GetResourceString("BrotliStream_Compress_UnsupportedOperation", @"Can not perform Read operations on a BrotliStream constructed with CompressionMode.Compress.");
        /// <summary>Encoder ran into invalid data.</summary>
        internal static string @BrotliStream_Compress_InvalidData => GetResourceString("BrotliStream_Compress_InvalidData", @"Encoder ran into invalid data.");
        /// <summary>Can not perform Write operations on a BrotliStream constructed with CompressionMode.DecodeData.</summary>
        internal static string @BrotliStream_Decompress_UnsupportedOperation => GetResourceString("BrotliStream_Decompress_UnsupportedOperation", @"Can not perform Write operations on a BrotliStream constructed with CompressionMode.Decompress.");
        /// <summary>Decoder ran into invalid data.</summary>
        internal static string @BrotliStream_Decompress_InvalidData => GetResourceString("BrotliStream_Decompress_InvalidData", @"Decoder ran into invalid data.");
        /// <summary>BrotliStream.BaseStream returned more bytes than requested in Read.</summary>
        internal static string @BrotliStream_Decompress_InvalidStream => GetResourceString("BrotliStream_Decompress_InvalidStream", @"BrotliStream.BaseStream returned more bytes than requested in Read.");
        /// <summary>Found truncated data while decoding.</summary>
        internal static string @BrotliStream_Decompress_TruncatedData => GetResourceString("BrotliStream_Decompress_TruncatedData", @"Found truncated data while decoding.");
        /// <summary>System.IO.Compression.Brotli is not supported on this platform.</summary>
        internal static string @IOCompressionBrotli_PlatformNotSupported => GetResourceString("IOCompressionBrotli_PlatformNotSupported", @"System.IO.Compression.Brotli is not supported on this platform.");

    }
}
