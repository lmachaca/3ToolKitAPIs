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
    string? lastPhotoPath;


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
    async void OnTakePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlertAsync("Info", "No camera on this device", "OK");
                return;
            }

            FileResult? photo = await MediaPicker.Default.CapturePhotoAsync();
            await ShowPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Camera error", ex.Message, "OK");
        }
    }

    async void OnPickPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            FileResult? photo = await MediaPicker.Default.PickPhotoAsync();
            await ShowPhotoAsync(photo);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Gallery error", ex.Message, "OK");
        }
    }
    async Task ShowPhotoAsync(FileResult? photo)
    {
        if (photo is null) return;   // user cancelled

        // Copy the photo to our app cache, then show it
        lastPhotoPath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
        using var source = await photo.OpenReadAsync();
        using var target = File.OpenWrite(lastPhotoPath);
        await source.CopyToAsync(target);

        PhotoImage.Source = ImageSource.FromFile(lastPhotoPath);
    }

    // Save the photo with the toolkit FileSaver
    async void OnSavePhotoClicked(object? sender, EventArgs e)
    {
        if (lastPhotoPath is null)
        {
            await DisplayAlertAsync("Info", "Take a photo first!", "OK");
            return;
        }

        using var stream = File.OpenRead(lastPhotoPath);
        var result = await fileSaver.SaveAsync(
            Path.GetFileName(lastPhotoPath), stream, CancellationToken.None);

        StatusLabel.Text = result.IsSuccessful
            ? $"Photo saved to: {result.FilePath}"
            : $"Not saved: {result.Exception?.Message}";
    }


}
