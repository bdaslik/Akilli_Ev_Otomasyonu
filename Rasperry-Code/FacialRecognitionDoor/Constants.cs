namespace FacialRecognitionDoor
{
    /// <summary>
    /// General constant variables
    /// </summary>
    public static class GeneralConstants
    {
        // This variable should be set to false for devices, unlike the Raspberry Pi, that have GPU support
        public const bool DisableLiveCameraFeed = true;

        // Oxford Face API Primary should be entered here
        // You can obtain a subscription key for Face API by following the instructions here: https://www.microsoft.com/cognitive-services/en-us/sign-up
        public const string OxfordAPIKey = "79e0c1ce1f904367b0ead7f262d654c8";
        
        // Name of the folder in which all Whitelist data is stored
        public const string WhiteListFolderName = "Yuz Tanima Beyaz Liste";

    }

    /// <summary>
    /// Constant variables that hold messages to be read via the SpeechHelper class
    /// </summary>
    public static class SpeechContants
    {
        public const string InitialGreetingMessage = "Yuz tanima sistemine hosgeldiniz.";

        public const string VisitorNotRecognizedMessage = "Uzgunum yuzunuz tanimlanamadi.";
        public const string NoCameraMessage = "Cihaza bagli bir kamera bulunamadi.";

        public static string GeneralGreetigMessage(string visitorName)
        {
            return visitorName + "Senin icin kapiyi aciyorum.";
        }
    }

    /// <summary>
    /// Constant variables that hold values used to interact with device Gpio
    /// </summary>
    public static class GpioConstants
    {
        // The GPIO pin that the doorbell button is attached to
        public const int ButtonPinID = 5;

        // The GPIO pin that the door lock is attached to
        public const int DoorLockPinID = 4;

        // The amount of time in seconds that the door will remain unlocked for
        public const int DoorLockOpenDurationSeconds = 10;
    }
}
