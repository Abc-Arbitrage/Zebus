namespace Abc.Zebus.Directory.Etcd
{
    public interface IEtcdConfiguration : IBusConfiguration
    {
        /// <summary>
        /// Prefix to use for all keys in `etcd`
        /// </summary>
        string Prefix { get; }

        /// <summary>
        /// Username to authenticate to `etcd`
        /// </summary>
        string Username { get; }

        /// <summary>
        /// Password to authenticate to `etcd`
        /// </summary>
        string Password { get; }
    }
}
