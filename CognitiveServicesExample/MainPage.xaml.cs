﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Graphics.Imaging;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Common.Contract;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.Popups;
using Windows.Storage.Streams;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CognitiveServicesExample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        //private string _subscriptionKey = "7f503ea7b7314a08aa908645a9488996";
        private string _subscriptionKey;
       
        private MediaCapture mediaCapture;
        private StorageFile photoFile;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private bool isPreviewing;
        private bool isRecording;

        BitmapImage bitMapImage;

        #region HELPER_FUNCTIONS

        enum Action
        {
            ENABLE,
            DISABLE
        }
        /// <summary>
        /// Helper function to enable or disable Initialization buttons
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetInitButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                video_init.IsEnabled = true;
            }
            else
            {
                video_init.IsEnabled = false;
            }
        }

        /// <summary>
        /// Helper function to enable or disable video related buttons (TakePhoto, Start Video Record)
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetVideoButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                takePhoto.IsEnabled = true;
                takePhoto.Visibility = Visibility.Visible;
            }
            else
            {
                takePhoto.IsEnabled = false;
                takePhoto.Visibility = Visibility.Collapsed;
            }
        }

        #endregion
        public MainPage()
        {
            this.InitializeComponent();
            SetInitButtonVisibility(Action.ENABLE);
            SetVideoButtonVisibility(Action.DISABLE);

            isRecording = false;
            isPreviewing = false;

        }
        /// <summary>
        /// 'Initialize Audio and Video' button action function
        /// Dispose existing MediaCapture object and set it up for audio and video
        /// Enable or disable appropriate buttons
        /// - DISABLE 'Initialize Audio and Video' 
        /// - DISABLE 'Start Audio Record'
        /// - ENABLE 'Initialize Audio Only'
        /// - ENABLE 'Start Video Record'
        /// - ENABLE 'Take Photo'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void initVideo_Click(object sender, RoutedEventArgs e)
        {
            // Disable all buttons until initialization completes

            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);

            try
            {
                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (isPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        captureImage.Source = null;
                        isPreviewing = false;
                    }
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                status.Text = "Initializing camera to capture audio and video...";
                // Use default initialization
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync();

                // Set callbacks for failure and recording limit exceeded
                status.Text = "Device successfully initialized for video recording!";
                mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);

                // Start Preview                
                previewElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
                status.Text = "Camera preview succeeded";

                // Enable buttons for video and photo capture
                SetVideoButtonVisibility(Action.ENABLE);
            }
            catch (Exception ex)
            {
                status.Text = "Unable to initialize camera for audio/video mode: " + ex.Message;
            }
        }

        private async void Cleanup()
        {
            if (mediaCapture != null)
            {
                // Cleanup MediaCapture object
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                    captureImage.Source = null;
                    isPreviewing = false;
                }
                mediaCapture.Dispose();
                mediaCapture = null;
            }
            SetInitButtonVisibility(Action.ENABLE);
        }
        private async void takePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                takePhoto.IsEnabled = false;
                captureImage.Source = null;

                //photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(PHOTO_FILE_NAME, CreationCollisionOption.ReplaceExisting);
                var folder = ApplicationData.Current.LocalFolder;
                photoFile = await folder.CreateFileAsync(PHOTO_FILE_NAME, CreationCollisionOption.ReplaceExisting);

                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);
                takePhoto.IsEnabled = true;
                status.Text = "Take Photo succeeded: " + photoFile.Path;

                IRandomAccessStream photoStream = await photoFile.OpenReadAsync();
                BitmapImage bitmap = new BitmapImage();
                bitmap.SetSource(photoStream);
                captureImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                Cleanup();
            }
            finally
            {
                takePhoto.IsEnabled = true;
            }
        }

        private async void mediaCapture_Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    status.Text = "MediaCaptureFailed: " + currentFailure.Message;

                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        status.Text += "\n Recording Stopped";
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    SetInitButtonVisibility(Action.DISABLE);
                    SetVideoButtonVisibility(Action.DISABLE);
                    status.Text += "\nCheck if camera is diconnected. Try re-launching the app";
                }
            });
        }


        /// <summary>
        /// Uploads the image to Project Oxford and detect emotions.
        /// </summary>
        /// <param name="imageFilePath">The image file path.</param>
        /// <returns></returns>

        private async Task<Emotion[]> UploadAndDetectEmotions(StorageFile result)
        {
            Debug.WriteLine("EmotionServiceClient is created");

            //
            // Create Project Oxford Emotion API Service client
            //
            _subscriptionKey = cognitivekey.Text;
            EmotionServiceClient emotionServiceClient = new EmotionServiceClient(_subscriptionKey);

            Debug.WriteLine("Calling EmotionServiceClient.RecognizeAsync()...");



            try
            {
                //
                // Detect the emotions in the URL
                //
                Emotion[] emotionResult;
                var stream = await result.OpenReadAsync();

                //using (Stream imageFileStream = File.OpenRead(url))
                using (Stream imageFileStream = stream.AsStream())
                {
                    emotionResult = await emotionServiceClient.RecognizeAsync(imageFileStream);
                    return emotionResult;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Detection failed. Please make sure that you have the right subscription key and proper URL to detect.");
                Debug.WriteLine(exception.ToString());
                return null;
            }
        }
        private async void button_Clicked(object sender, RoutedEventArgs e)
        {
            string urlString = "C:\\Data\\Users\\photo.jpg";
            Uri uri;
            try
            {
                uri = new Uri(urlString);
            }
            catch (UriFormatException ex)
            {
                Debug.WriteLine(ex.Message);

                var dialog = new MessageDialog("URL is not correct");

                await dialog.ShowAsync();

                return;
            }

            var folder = ApplicationData.Current.LocalFolder;
            var files = await  folder.GetFilesAsync();
            var result = files.FirstOrDefault(x => x.Name == "photo.jpg");

            //Load image from URL
            bitMapImage = new BitmapImage();

            bitMapImage.UriSource = uri;
            //StorageFile bitmapfile = await folder.GetFileAsync("photo.jpg");
            //IRandomAccessStream filestream = await bitmapfile.OpenReadAsync();
            //bitMapImage.SetSource(filestream);

            //Load image to UI
            //ImageCanvas.Background = imageBrush;

            status.Text = "Detecting...";

            Emotion[] emotionResult = await UploadAndDetectEmotions(result);
            if (emotionResult != null) { 
                status.Text = "Detection Done";

                displayParsedResults(emotionResult);
                displayAllResults(emotionResult);
                DrawFaceRectangle(emotionResult, bitMapImage, urlString);
            }
            else
            {
                status.Text = "Detection Failed";
            }
        }

        private void reset_Click(object sender, RoutedEventArgs e)
        {
            Cleanup();
            int count = PhotoCanvas.Children.Count();

            while (count > 1 ) { 
                PhotoCanvas.Children.RemoveAt(count-1);
                count--;
            }
            ResultBox.Items.Clear();
        }
        private void displayAllResults(Emotion[] resultList)
        {
            int index = 0;
            foreach (Emotion emotion in resultList)
            {
                ResultBox.Items.Add("\nFace #" + index
                    + "\nAnger: " + emotion.Scores.Anger
                    + "\nContempt: " + emotion.Scores.Contempt
                    + "\nDisgust: " + emotion.Scores.Disgust
                    + "\nFear: " + emotion.Scores.Fear
                    + "\nHappiness: " + emotion.Scores.Happiness
                    + "\nSurprise: " + emotion.Scores.Surprise);

                index++;
            }
        }

        private async void displayParsedResults(Emotion[] resultList)
        {
            int index = 0;
            string textToDisplay = "";
            foreach (Emotion emotion in resultList)
            {
                string emotionString = parseResults(emotion);
                textToDisplay += "Person number " + index.ToString() + " " + emotionString + "\n";
                index++;
            }
            ResultBox.Items.Add(textToDisplay);
        }

        private string parseResults(Emotion emotion)
        {
            float topScore = 0.0f;
            string topEmotion = "";
            string retString = "";
            //anger
            topScore = emotion.Scores.Anger;
            topEmotion = "Anger";
            // contempt
            if (topScore < emotion.Scores.Contempt)
            {
                topScore = emotion.Scores.Contempt;
                topEmotion = "Contempt";
            }
            // disgust
            if (topScore < emotion.Scores.Disgust)
            {
                topScore = emotion.Scores.Disgust;
                topEmotion = "Disgust";
            }
            // fear
            if (topScore < emotion.Scores.Fear)
            {
                topScore = emotion.Scores.Fear;
                topEmotion = "Fear";
            }
            // happiness
            if (topScore < emotion.Scores.Happiness)
            {
                topScore = emotion.Scores.Happiness;
                topEmotion = "Happiness";
            }
            // surprise
            if (topScore < emotion.Scores.Surprise)
            {
                topScore = emotion.Scores.Surprise;
                topEmotion = "Surprise";
            }

            retString = "is expressing " + topEmotion + " with " + topScore.ToString() + " certainty.";
            return retString;
        }

        public async void DrawFaceRectangle(Emotion[] emotionResult, BitmapImage bitMapImage, String urlString)
        {


            if (emotionResult != null && emotionResult.Length > 0)
            {
                var folder = ApplicationData.Current.LocalFolder;
                photoFile = await folder.GetFileAsync("photo.jpg");
                IRandomAccessStream stream = await photoFile.OpenReadAsync();

                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                double resizeFactorH = PhotoCanvas.Height / decoder.PixelHeight;
                double resizeFactorW = PhotoCanvas.Width / decoder.PixelWidth;

                foreach (var emotion in emotionResult)
                {

                    Microsoft.ProjectOxford.Common.Rectangle faceRect = emotion.FaceRectangle;

                    Image Img = new Image();
                    BitmapImage BitImg = new BitmapImage();
                    // open the rectangle image, this will be our face box
                    Windows.Storage.Streams.IRandomAccessStream box = await Windows.Storage.Streams.RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/rectangle.png", UriKind.Absolute)).OpenReadAsync();

                    BitImg.SetSource(box);

                    //rescale each facebox based on the API's face rectangle
                    var maxWidth = faceRect.Width * resizeFactorW;
                    var maxHeight = faceRect.Height * resizeFactorH;

                    var origHeight = BitImg.PixelHeight;
                    var origWidth = BitImg.PixelWidth;


                    var ratioX = maxWidth / (float)origWidth;
                    var ratioY = maxHeight / (float)origHeight;
                    var ratio = Math.Min(ratioX, ratioY);
                    var newHeight = (int)(origHeight * ratio);
                    var newWidth = (int)(origWidth * ratio);

                    BitImg.DecodePixelWidth = newWidth;
                    BitImg.DecodePixelHeight = newHeight;

                    // set the starting x and y coordiantes for each face box
                    Thickness margin = Img.Margin;

                    margin.Left = faceRect.Left * resizeFactorW;
                    margin.Top = faceRect.Top * resizeFactorH;

                    Img.Margin = margin;

                    Img.Source = BitImg;
                    PhotoCanvas.Children.Add(Img);

                }

            }
        }
    }
}
