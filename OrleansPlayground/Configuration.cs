public static class Configuration
{
    public static TimeSpan ReminderDue = TimeSpan.FromSeconds(0);
    public static TimeSpan ReminderPeriod = TimeSpan.FromSeconds(10);
    public static TimeSpan TimerPeriod = TimeSpan.FromSeconds(10);

    public const string ReminderGrainName = "remainder-grain";
    public const string ReminderAndTimerGrainName = "remainder-timer-grain";
}
