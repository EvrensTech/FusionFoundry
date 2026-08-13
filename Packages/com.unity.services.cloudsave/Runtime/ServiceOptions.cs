namespace Unity.Services.CloudSave
{
    /// <summary>
    /// Options for storing writelock
    /// </summary>
    public class WriteLockOptions
    {
        /// <summary>
        /// The writelock
        /// </summary>
        public string WriteLock { get; set; }
    }

    /// <summary>
    /// Options for save operations
    /// </summary>
    public class SaveOptions : WriteLockOptions
    {
        /// <summary>
        /// Request timeout in seconds for upload operations. If null, uses the default timeout.
        /// </summary>
        public int? RequestTimeout { get; set; }
    }

    /// <summary>
    /// Options for delete operations
    /// </summary>
    public class DeleteOptions : WriteLockOptions
    {
    }
}
