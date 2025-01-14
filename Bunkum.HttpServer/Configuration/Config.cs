using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NotEnoughLogs;

namespace Bunkum.HttpServer.Configuration;

/// <summary>
/// A custom configuration file for a Bunkum server.
/// If you're looking for Bunkum's base configuration, see <see cref="BunkumConfig"/>.
/// </summary>
[JsonObject(MemberSerialization.OptOut)]
public abstract class Config
{
    [JsonIgnore]
    private string? _filename;

    /// <summary>
    /// A number representing the schema version of your configuration. Start at 1, then change it when you make a change to your config class.
    /// </summary>
    [JsonIgnore]
    public abstract int CurrentConfigVersion { get; }
    
    /// <summary>
    /// The configuration's current schema version. Do not touch this - you do not need to.
    /// </summary>
    public abstract int Version { get; set; }

    /// <summary>
    /// Migrate a configuration. Called when the config is loaded and the current version is newer than the one in the configuration.
    /// </summary>
    /// <param name="oldVer">The version of the configuration we are migrating.</param>
    /// <param name="oldConfig">The configuration we are migrating, stored as dynamic.</param>
    protected internal abstract void Migrate(int oldVer, dynamic oldConfig);

    [JsonIgnore]
    private bool NeedsMigration => this.CurrentConfigVersion > this.Version;

    private void MigrateIfNecessary(string? jsonText = null)
    {
        if (!this.NeedsMigration) return;
        
        dynamic? configDynamic;
        if (jsonText != null)
        {
            configDynamic = JsonConvert.DeserializeObject<ExpandoObject>(jsonText, new ExpandoObjectConverter());
            if (configDynamic == null) throw new ArgumentNullException(nameof(configDynamic));
        }
        else
        {
            configDynamic = this;
        }
            
        this.Migrate(this.Version, configDynamic);
        this.Version = this.CurrentConfigVersion;
        
        // Save config to disk if possible
        this.SaveToFile();
    }

    private void SaveToFile()
    {
        if (this._filename == null) return;
        
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(this._filename, json);
    }

    internal static TConfig LoadFromFile<TConfig>(string filename, LoggerContainer<BunkumContext>? logger = null) where TConfig : Config, new()
    {
        TConfig? config;
        string? file = null;

        filename = Path.Combine(BunkumFileSystem.DataDirectory, filename);
        
        if (File.Exists(filename))
        {
            file = File.ReadAllText(filename);
            config = JsonConvert.DeserializeObject<TConfig>(file);
        }
        else
        {
            logger?.LogInfo(BunkumContext.Configuration, $"A new {typeof(TConfig).Name} is being created at {Path.GetFullPath(filename)}.");
            config = new TConfig();
        }
        
        if (config == null) throw new ArgumentNullException(nameof(filename));

        config._filename = filename;
        config.MigrateIfNecessary(file);

        return config;
    }
}