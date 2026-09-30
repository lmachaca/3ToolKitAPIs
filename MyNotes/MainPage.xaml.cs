using Android.Widget;
using CommunityToolkit.Maui.Media;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Alerts;
using System.Globalization;
using System.Text;



namespace MyNotes;

public partial class MainPage : ContentPage
{
    readonly IFolderPicker folderPicker;
    readonly IFileSaver fileSaver;
    readonly ISpeechToText speechToText;



    public MainPage(IFolderPicker folderPicker, IFileSaver fileSaver, ISpeechToText speechToText)
    {
        InitializeComponent();

        this.folderPicker = folderPicker;
        this.fileSaver = fileSaver;
        this.speechToText = speechToText;

        //suscribe once if something happens in the speech to text service, it will call the method below
        speechToText.RecognitionResultUpdated += OnTextUpdated;
        speechToText.RecognitionResultCompleted += OnTextCompleted;
        speechToText.StateChanged += OnStateChanged;

    }

    private async void OnPickFolderClicked(object? sender, EventArgs e)
    {
        var result = await folderPicker.PickAsync(CancellationToken.None);

        if (result.IsSuccessful)
        {

            FolderLabel.Text = $"Folder: {result.Folder.Name}";
        }
        else { 
         await DisplayAlertAsync("Folder selection was canceled or failed.", "Please try again.", "OK");

        }

    }
    private async void OnSaveFileClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TranscriptEditor.Text))
        {
            await DisplayAlertAsync("Info", "Say something first!", "OK");
            return;
        }

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(TranscriptEditor.Text));
        var fileName = $"note-{DateTime.Now:yyyyMMdd-HHmmss}.txt";

        var result = await fileSaver.SaveAsync(
            fileName, stream, CancellationToken.None);

        StatusLabel.Text = result.IsSuccessful
            ? $"Saved to: {result.FilePath}"
            : $"Not saved: {result.Exception?.Message}";

    }
    private async void OnListenClicked(object? sender, EventArgs e)
    {
        try
        {
            // If already listening, this click means STOP
            if (speechToText.CurrentState == SpeechToTextState.Listening)
            {
                await speechToText.StopListenAsync(CancellationToken.None);
                return;
            }

            var granted = await speechToText.RequestPermissions(
                CancellationToken.None);
            if (!granted)
            {
                await DisplayAlertAsync("Permission",
                    "Microphone permission was not granted", "OK");
                return;
            }

            await speechToText.StartListenAsync(new SpeechToTextOptions
            {
                Culture = new CultureInfo("en-US"),   
                ShouldReportPartialResults = true     
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Speech error", ex.Message, "OK");
        }


    }

    void OnStateChanged(object? sender, SpeechToTextStateChangedEventArgs e) =>
       MainThread.BeginInvokeOnMainThread(() =>
       {
           ListenButton.Text = e.State == SpeechToTextState.Listening
               ? "Stop listening" : "Start listening";
           StateLabel.Text = $"State: {e.State}";
       });

    void OnTextUpdated(object? sender,
        SpeechToTextRecognitionResultUpdatedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
            TranscriptEditor.Text = e.RecognitionResult);

    void OnTextCompleted(object? sender,
        SpeechToTextRecognitionResultCompletedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.RecognitionResult.IsSuccessful)
                TranscriptEditor.Text = e.RecognitionResult.Text;
        });

}
