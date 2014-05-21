
namespace Abc.Zebus.EventSourcing
{
    /// <remarks>
    /// This class should stay in the Zebus project to avoid referencing Abc.Zebus.EventSourcing in Event/Command projects
    /// </remarks>
    public interface IDomainEvent : IEvent
    {
        DomainEventSourcing Sourcing { get; set; }
    }
}