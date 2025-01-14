namespace Bunkum.HttpServer.RateLimit.Info;

internal class RateLimitUserInfo : IRateLimitInfo
{
    internal RateLimitUserInfo(IRateLimitUser user)
    {
        this.User = user;
    }

    internal IRateLimitUser User { get; init; }
    public List<int> RequestTimes { get; init; } = new(RateLimitSettings.DefaultMaxRequestAmount / 2);
    public int LimitedUntil { get; set; }
}