using StructureMap;

namespace Abc.Zebus
{
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