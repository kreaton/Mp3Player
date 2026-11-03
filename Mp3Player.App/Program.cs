namespace Mp3Player;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // MusicPlayer raises its events through the SynchronizationContext current when it is created,
        // so make sure the Windows Forms one is in place.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        try
        {
            // Optional: a folder passed on the command line is opened and played right away.
            string? folder = args.FirstOrDefault(Directory.Exists);
            Application.Run(new MainForm(folder));
        }
        catch (Exception ex)
        {
            MessageBox.Show("The player could not start:\n\n" + ex.Message, "MP3 Player",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
