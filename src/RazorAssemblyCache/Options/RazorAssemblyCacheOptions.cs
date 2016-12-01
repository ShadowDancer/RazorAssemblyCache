namespace RazorAssemblyCache.Options
{
    /// <summary>
    ///     Configures razor assembly cache
    /// </summary>
    public class RazorAssemblyCacheOptions
    {
        /// <summary>
        ///     Directory to store cached assemblies
        ///     Cache directory mimics Views directory structure appending suffixes to view name
        ///     .dll for compiled assembly and .pdb for symbols
        ///     Path can be absolute or relative to contentRoot
        /// </summary>
        public string CacheDirectory { get; set; }
    }
}