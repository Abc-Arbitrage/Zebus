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
        public virtual int Priority => 0;

        public virtual void ConfigureContainer(IContainer container)
        {
        }

        /// <summary>
        /// Invoked before <see cref="IBus.Start"/>.
        /// </summary>
        public virtual void BeforeStart()
        {
        }

        /// <summary>
        /// Invoked during <see cref="IBus.Start"/>, after register but before starting dispatcher.
        /// </summary>
        public virtual void BeforeDispatcherStart()
        {
        }

        /// <summary>
        /// Invoked after <see cref="IBus.Start"/>.
        /// </summary>
        public virtual void AfterStart()
        {
        }

        /// <summary>
        /// Invoked before <see cref="IBus.Stop"/>.
        /// </summary>
        public virtual void BeforeStop()
        {
        }

        /// <summary>
        /// Invoked after <see cref="IBus.Stop"/>.
        /// </summary>
        public virtual void AfterStop()
        {
        }
    }
}
