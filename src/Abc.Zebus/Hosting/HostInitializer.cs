using StructureMap;

namespace Abc.Zebus.Hosting
{
    /// <summary>
    /// The host initializer is a mechanism that can be used to invoke code before
    /// and after the bus start and stop, provided you use HostInitializerHelper in your
    /// runtime implementation
    /// </summary>
    public abstract class HostInitializer
    {
        /// <summary>
        /// Defines the order in which HostInitializers will be executed. Set a higher number to execute first.
        /// </summary>
        public virtual int Priority
        {
            get { return 0; }
        }

        public virtual void ConfigureContainer(IContainer container)
        {
        }

        public virtual void BeforeStart()
        {
        }

        public virtual void AfterStart()
        {
        }

        public virtual void BeforeStop()
        {
        }

        public virtual void AfterStop()
        {
        }
    }
}