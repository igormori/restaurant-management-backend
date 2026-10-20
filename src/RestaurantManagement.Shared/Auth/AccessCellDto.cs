namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// One cell of an access grid: the level a member holds in a module at a location.
    /// A level of None is never a cell.
    /// </summary>
    public class AccessCellDto
    {
        public Guid LocationId { get; init; }

        public AccessModule Module { get; init; }

        public AccessLevel Level { get; init; }
    }
}
