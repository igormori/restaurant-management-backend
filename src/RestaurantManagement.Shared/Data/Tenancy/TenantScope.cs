namespace RestaurantManagement.Shared.Data.Tenancy
{
    public sealed class TenantScope : ITenantScope
    {
        private readonly AsyncLocal<ScopeFrame?> _current = new();

        public Guid? OrganizationId => FirstOpen(_current.Value)?.OrganizationId;

        public IDisposable Begin(Guid organizationId)
        {
            var frame = new ScopeFrame(this, organizationId, _current.Value);
            _current.Value = frame;
            return frame;
        }

        private static ScopeFrame? FirstOpen(ScopeFrame? frame)
        {
            while (frame is not null && frame.IsDisposed)
                frame = frame.Parent;

            return frame;
        }

        // Frames form a stack so that disposing twice, or disposing an outer scope before an inner one,
        // never brings back an organization whose scope already ended.
        private sealed class ScopeFrame : IDisposable
        {
            private readonly TenantScope _owner;

            public ScopeFrame(TenantScope owner, Guid organizationId, ScopeFrame? parent)
            {
                _owner = owner;
                OrganizationId = organizationId;
                Parent = parent;
            }

            public Guid OrganizationId { get; }
            public ScopeFrame? Parent { get; }
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                if (IsDisposed)
                    return;

                IsDisposed = true;
                if (_owner._current.Value == this)
                    _owner._current.Value = FirstOpen(Parent);
            }
        }
    }
}
