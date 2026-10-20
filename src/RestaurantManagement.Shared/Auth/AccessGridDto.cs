namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// A user's access in one organization: the Owner flag (full access everywhere, no cells
    /// needed) and the stored cells (location x module -> level) of that organization only.
    /// </summary>
    public class AccessGridDto
    {
        public bool IsOwner { get; init; }

        public IReadOnlyList<AccessCellDto> Cells { get; init; } = Array.Empty<AccessCellDto>();
    }
}
