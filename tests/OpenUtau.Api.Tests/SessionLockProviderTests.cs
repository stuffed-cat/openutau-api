using OpenUtau.Api.Services;

namespace OpenUtau.Api.Tests
{
    public class SessionLockProviderTests
    {
        [Fact]
        public void SameSessionSharesLock()
        {
            var first = SessionLockProvider.GetLock("session-a");
            var second = SessionLockProvider.GetLock("session-a");

            Assert.Same(first, second);
        }

        [Fact]
        public void DifferentSessionsUseDifferentLocks()
        {
            var first = SessionLockProvider.GetLock("session-a");
            var second = SessionLockProvider.GetLock("session-b");

            Assert.NotSame(first, second);
        }

        [Fact]
        public void EmptySessionFallsBackToGlobalLock()
        {
            var first = SessionLockProvider.GetLock(null);
            var second = SessionLockProvider.GetLock(string.Empty);

            Assert.Same(first, second);
        }
    }
}