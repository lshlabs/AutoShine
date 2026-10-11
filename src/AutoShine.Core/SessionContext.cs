namespace AutoShine.Core;

public static class SessionContext
{
    public static SunshineSettings Connection(AudioSessionSnapshot? session, AppSettings current) =>
        session?.Connection ?? current.Sunshine;
}
