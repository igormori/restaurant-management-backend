using Microsoft.Extensions.Localization;
using NSubstitute;
using RestaurantManagement.Shared;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Builds a fake IStringLocalizer&lt;SharedResource&gt; that echoes the requested key back
    /// as the localized value, since the tests assert on BusinessException status codes,
    /// not on translated message text.
    /// </summary>
    public static class TestLocalizer
    {
        public static IStringLocalizer<SharedResource> Create()
        {
            var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
            localizer[Arg.Any<string>()].Returns(callInfo => new LocalizedString(callInfo.Arg<string>(), callInfo.Arg<string>()));
            return localizer;
        }
    }
}
