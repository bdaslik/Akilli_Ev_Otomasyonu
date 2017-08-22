using FacialRecognitionDoor.FacialRecognition;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using FacialRecognitionDoor.Helpers;
using FacialRecognitionDoor.Objects;
using Microsoft.ProjectOxford.Face;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using System.Net.Http;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FacialRecognitionDoor
{
    public sealed partial class MainPage : Page
    {
        // Webcam Related Variables:
        private WebcamHelper webcam;

        // Oxford Related Variables:
        private bool initializedOxford = false;

        // Whitelist Related Variables:
        private List<Visitor> whitelistedVisitors = new List<Visitor>();
        private StorageFolder whitelistFolder;
        private bool currentlyUpdatingWhitelist;

        // Speech Related Variables:
        private SpeechHelper speech;

        // GPIO Related Variables:
        private GpioHelper gpioHelper;
        private bool gpioAvailable;
        private bool doorbellJustPressed = false;

        // GUI Related Variables:
        private double visitorIDPhotoGridMaxWidth = 0;
        

        private I2cDevice Device;

        private Timer periodicTimer;
        


        public MainPage()
        {
            InitializeComponent();

            initcomunica();

            // Causes this page to save its state when navigating to other pages
            NavigationCacheMode = NavigationCacheMode.Enabled;

            if (initializedOxford == false)
            {
                // If Oxford facial recognition has not been initialized, attempt to initialize it
                InitializeOxford();
            }

            if(gpioAvailable == false)
            {
                // If GPIO is not available, attempt to initialize it
                InitializeGpio();
            }

            // If user has set the DisableLiveCameraFeed within Constants.cs to true, disable the feed:
            if(GeneralConstants.DisableLiveCameraFeed)
            {
                LiveFeedPanel.Visibility = Visibility.Collapsed;
                DisabledFeedGrid.Visibility = Visibility.Visible;
            }
            else
            {
                LiveFeedPanel.Visibility = Visibility.Visible;
                DisabledFeedGrid.Visibility = Visibility.Collapsed;
            }
        }
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if(initializedOxford)
            {
                UpdateWhitelistedVisitors();
            }
        }
        
        public async void InitializeOxford()
        {
            // initializedOxford bool will be set to true when Oxford has finished initialization successfully
            initializedOxford = await OxfordFaceAPIHelper.InitializeOxford();

            // Populates UI grid with whitelisted visitors
            UpdateWhitelistedVisitors();
        }
        
        public void InitializeGpio()
        {
            try
            {
                // Attempts to initialize application GPIO. 
                gpioHelper = new GpioHelper();
                gpioAvailable = gpioHelper.Initialize();
            }
            catch
            {
                // This can fail if application is run on a device, such as a laptop, that does not have a GPIO controller
                gpioAvailable = false;
                Debug.WriteLine("GPIO controller not available.");
            }

            // If initialization was successfull, attach doorbell pressed event handler
            if (gpioAvailable)
            {
                gpioHelper.GetDoorBellPin().ValueChanged += DoorBellPressed;
            }
        }
        
        private async void WebcamFeed_Loaded(object sender, RoutedEventArgs e)
        {
            if (webcam == null || !webcam.IsInitialized())
            {
                // Initialize Webcam Helper
                webcam = new WebcamHelper();
                await webcam.InitializeCameraAsync();

                // Set source of WebcamFeed on MainPage.xaml
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    // Start the live feed
                    await webcam.StartCameraPreview();
                }
            }
            else if(webcam.IsInitialized())
            {
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    await webcam.StartCameraPreview();
                }
            }
        }
        
        private async void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech == null)
            {
                speech = new SpeechHelper(speechMediaElement);
                await speech.Read(SpeechContants.InitialGreetingMessage);
            }
            else
            {
                // Prevents media element from re-greeting visitor
                speechMediaElement.AutoPlay = false;
            }
        }
        
        private void WhitelistedUsersGrid_Loaded(object sender, RoutedEventArgs e)
        {
            visitorIDPhotoGridMaxWidth = (WhitelistedUsersGrid.ActualWidth / 3) - 10;
        }
        
        private async void DoorBellPressed(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (!doorbellJustPressed)
            {
                // Checks to see if even was triggered from a press or release of button
                if (args.Edge == GpioPinEdge.FallingEdge)
                {
                    //Doorbell was just pressed
                    doorbellJustPressed = true;

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        await DoorbellPressed();
                    });

                }
            }
        }
        
        private async void DoorbellButton_Click(object sender, RoutedEventArgs e)
        {
            if (!doorbellJustPressed)
            {
                doorbellJustPressed = true;
                await DoorbellPressed();
            }
        }
        
        private async Task DoorbellPressed()
        {
            // Display analysing visitors grid to inform user that doorbell press was registered
            AnalysingVisitorGrid.Visibility = Visibility.Visible;

            // List to store visitors recognized by Oxford Face API
            // Count will be greater than 0 if there is an authorized visitor at the door
            List<string> recognizedVisitors = new List<string>();

            // Confirms that webcam has been properly initialized and oxford is ready to go
            if (webcam.IsInitialized() && initializedOxford)
            {
                // Stores current frame from webcam feed in a temporary folder
                StorageFile image = await webcam.CapturePhoto();

                try
                {
                    // Oxford determines whether or not the visitor is on the Whitelist and returns true if so
                    recognizedVisitors = await OxfordFaceAPIHelper.IsFaceInWhitelist(image);                    
                }
                catch (FaceRecognitionException fe)
                {
                    switch (fe.ExceptionType)
                    {
                        // Fails and catches as a FaceRecognitionException if no face is detected in the image
                        case FaceRecognitionExceptionType.NoFaceDetected:
                            Debug.WriteLine("UYARI: Yuz tespit edilemedi.");
                            break;
                    }
                }
                catch (FaceAPIException faceAPIEx)
                {
                    Debug.WriteLine("FaceAPIException in IsFaceInWhitelist(): " + faceAPIEx.ErrorMessage);
                }
                catch
                {
                    // General error. This can happen if there are no visitors authorized in the whitelist
                    Debug.WriteLine("WARNING: Oxford just threw a general expception.");
                }
                
                if(recognizedVisitors.Count > 0)
                {
                    // If everything went well and a visitor was recognized, unlock the door:
                    UnlockDoor(recognizedVisitors[0]);
                }
                else
                {
                    // Otherwise, inform user that they were not recognized by the system
                    await speech.Read(SpeechContants.VisitorNotRecognizedMessage);
                }
            }
            else
            {
                if (!webcam.IsInitialized())
                {
                    // The webcam has not been fully initialized for whatever reason:
                    Debug.WriteLine("Kullanilabilir kamera bulunamadi!!");
                    await speech.Read(SpeechContants.NoCameraMessage);
                }

                if(!initializedOxford)
                {
                    // Oxford is still initializing:
                    Debug.WriteLine("Unable to analyze visitor at door as Oxford Facial Recogntion is still initializing.");
                }
            }

            doorbellJustPressed = false;
            AnalysingVisitorGrid.Visibility = Visibility.Collapsed;
        }
        
        private async void UnlockDoor(string visitorName)
        {
            // Greet visitor
            await speech.Read(SpeechContants.GeneralGreetigMessage(visitorName));
            sendName(visitorName);

            if(gpioAvailable)
            {
                // Unlock door for specified ammount of time
                gpioHelper.UnlockDoor();
            }
        }
        
        private async void NewUserButton_Click(object sender, RoutedEventArgs e)
        { 
            // Stops camera preview on this page, so that it can be started on NewUserPage
            await webcam.StopCameraPreview();

            //Navigates to NewUserPage, passing through initialized WebcamHelper object
            Frame.Navigate(typeof(NewUserPage), webcam);
        }
        
        private async void UpdateWhitelistedVisitors()
        {
            // If the whitelist isn't already being updated, update the whitelist
            if (!currentlyUpdatingWhitelist)
            {
                currentlyUpdatingWhitelist = true;
                await UpdateWhitelistedVisitorsList();
                UpdateWhitelistedVisitorsGrid();
                currentlyUpdatingWhitelist = false;
            }
        }
        
        private async Task UpdateWhitelistedVisitorsList()
        {
            // Clears whitelist
            whitelistedVisitors.Clear();

            // If the whitelistFolder has not been opened, open it
            if (whitelistFolder == null)
            {
                whitelistFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.WhiteListFolderName, CreationCollisionOption.OpenIfExists);
            }

            // Populates subFolders list with all sub folders within the whitelist folders.
            // Each of these sub folders represents the Id photos for a single visitor.
            var subFolders = await whitelistFolder.GetFoldersAsync();

            // Iterate all subfolders in whitelist
            foreach (StorageFolder folder in subFolders)
            {
                string visitorName = folder.Name;
                var filesInFolder = await folder.GetFilesAsync();

                var photoStream = await filesInFolder[0].OpenAsync(FileAccessMode.Read);
                BitmapImage visitorImage = new BitmapImage();
                await visitorImage.SetSourceAsync(photoStream);

                Visitor whitelistedVisitor = new Visitor(visitorName, folder, visitorImage, visitorIDPhotoGridMaxWidth);

                whitelistedVisitors.Add(whitelistedVisitor);
            }
        }
        
        private void UpdateWhitelistedVisitorsGrid()
        {
            // Reset source to empty list
            WhitelistedUsersGrid.ItemsSource = new List<Visitor>();
            // Set source of WhitelistedUsersGrid to the whitelistedVisitors list
            WhitelistedUsersGrid.ItemsSource = whitelistedVisitors;

            // Hide Oxford loading ring
            OxfordLoadingRing.Visibility = Visibility.Collapsed;
        }
        
        private void WhitelistedUsersGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to UserProfilePage, passing through the selected Visitor object and the initialized WebcamHelper as a parameter
            Frame.Navigate(typeof(UserProfilePage), new UserProfileObject(e.ClickedItem as Visitor, webcam));
        }
        
        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit app
            Application.Current.Exit();
        }

        private void WelcomeBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }




        private async void initcomunica()
        {

            var settings = new I2cConnectionSettings(0x40); // Arduino address

            settings.BusSpeed = I2cBusSpeed.FastMode;

            string aqs = I2cDevice.GetDeviceSelector("I2C1");

            var dis = await DeviceInformation.FindAllAsync(aqs);

            Device = await I2cDevice.FromIdAsync(dis[0].Id, settings);

            periodicTimer = new Timer(this.TimerCallback, null, 0, 2000); // Create a timmer

        }

        private void TimerCallback(object state)
        {
            byte[] RegAddrBuf = new byte[] { 0x40 };

            byte[] ReadBuf = new byte[1000];

            try

            {
                Device.Read(ReadBuf); // read the data
            }
            catch (Exception f)
            {
                Debug.WriteLine(f.Message);
            }

            char[] cArray = System.Text.Encoding.UTF8.GetString(ReadBuf, 0, ReadBuf.Length).ToCharArray();  // Converte  Byte to Char

            String c = new String(cArray);

            if (Array.IndexOf(cArray, '�') != -1)
            {
                c = new String(cArray, 0, Array.IndexOf(cArray, '�'));
            }
            Debug.WriteLine(c);

            // refresh the screen, note Im using a textbock @ UI

            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>

            { arduino.Text = c; });
            if (c.IndexOf(';')>0)
            {
                sendData(c);
            }
            
        }

        public async void sendData(string data)
        {
            
            using (var client = new HttpClient())
            {
                string api_key = "IFG5HF0AGNE7KFFD";

                string[] token = data.Split(';');

                string tx_temp = token[0];
                string tx_hum = token[1];
                string tx_mesafe = token[2];
                string tx_LDR = token[3];
                string tx_gaz = token[4];
                string tx_su = token[5];
               

                client.BaseAddress = new Uri("https://api.thingspeak.com/");

                var keyValues = new List<KeyValuePair<string, string>>();
                keyValues.Add(new KeyValuePair<string, string>("key", api_key));
                keyValues.Add(new KeyValuePair<string, string>("field1", tx_temp));
                keyValues.Add(new KeyValuePair<string, string>("field2", tx_hum));
                keyValues.Add(new KeyValuePair<string, string>("field3", tx_LDR));
                keyValues.Add(new KeyValuePair<string, string>("field4", tx_mesafe));
                keyValues.Add(new KeyValuePair<string, string>("field5", tx_gaz));
                keyValues.Add(new KeyValuePair<string, string>("field6", tx_su));

                if (token[6] != "Null")
                {
                    keyValues.Add(new KeyValuePair<string, string>("field7", token[6]));
                }

                var formData = new FormUrlEncodedContent(keyValues);

                var result = client.PostAsync("update", formData).Result;
                var content = result.Content.ReadAsStringAsync().Result;
                string resultContent = result.Content.ReadAsStringAsync().Result;

            }
        }

        public async void sendName(string data)
        {
            using (var client = new HttpClient())
            {
                string api_key = "IFG5HF0AGNE7KFFD";

                string tx_Name = data;

                client.BaseAddress = new Uri("https://api.thingspeak.com/");

                var keyValues = new List<KeyValuePair<string, string>>();
                keyValues.Add(new KeyValuePair<string, string>("key", api_key));
                keyValues.Add(new KeyValuePair<string, string>("field7", tx_Name));

                var formData = new FormUrlEncodedContent(keyValues);

                var result = client.PostAsync("update", formData).Result;
                var content = result.Content.ReadAsStringAsync().Result;
                string resultContent = result.Content.ReadAsStringAsync().Result;

            }
        }
    }
}
