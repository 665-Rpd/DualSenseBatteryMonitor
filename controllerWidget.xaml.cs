using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DualSenseBatteryMonitor
{
    public partial class controllerWidget : Page
    {
        //Controller index (1-based)
        private int self_index;

        //Opacity values for connected and disconnected states
        private const float enabled_opacity = 0.7f;
        private const float disabled_opacity = 0.4f; //Kinda deprecated

        //Player colors
        private LinearGradientBrush color_player_01 = new LinearGradientBrush();
        private LinearGradientBrush color_player_02 = new LinearGradientBrush();
        private LinearGradientBrush color_player_03 = new LinearGradientBrush();
        private LinearGradientBrush color_player_04 = new LinearGradientBrush();
        private List<Brush> playerColors;

        //Controls animation for low battery status
        private bool isPlayingLowBatAnim = false;
        //Low battery anim threshold
        private const int lowBatteryThreshold = 15;
        //cached storyboard reference
        private Storyboard blink_storyboard;

        // Constructor
        public controllerWidget(bool noControllers, int index, int batterylevel)
        {
            self_index = index + 1;

            InitializePlayerColors();
            InitializeComponent();

            //Load animation from XAML resources
            blink_storyboard = (Storyboard)Resources["BlinkStoryboard"];

            //Apply player-specific color to index text
            updatePlayerColor();

            //Set index in text block
            count_index.Text = self_index.ToString();
        }

        //Updates the widget based on controller count, battery level, and charging status
        public void RefreshData(int controllerAmount, int batterylevel, bool isCharging)
        {
            //no Controllers
            if (controllerAmount == 0)
            {
                if (self_index != 0) 
                {
                    //Hide widget for unused player slots
                    Visibility = Visibility.Collapsed;
                    //If widget is hidden, no need to update the icons

                    UpdateBatteryAnim(0, true); //Stop blinking to improve performance
                } else
                {
                    //No controllers, show "disconnected" icon
                    //Will still not be visible as the application is not visible when no dualsense controllers are plugged in, kinda useless code now :\
                    Visibility = Visibility.Visible;
                    image_controller_icon.Source = new BitmapImage(new Uri(@"/icons/controller/dualsense_not_connected.png", UriKind.Relative));
                    //Make icon darker when no controller is connected
                    image_controller_icon.Opacity = disabled_opacity;

                    progressbar_battery.Value = 0;
                    progressbar_battery.Foreground = new System.Windows.Media.SolidColorBrush(ColorFromHue(0));

                    SetChargingIconActive(isCharging);

                    UpdateBatteryAnim(batterylevel, isCharging);
                }
            }
            else
            {
                //If this controller is connected
                if (controllerAmount >= self_index)
                {
                    //Show controller as connected
                    Visibility = Visibility.Visible;

                    image_controller_icon.Source = new BitmapImage(new Uri(@"/icons/controller/dualsense_connected.png", UriKind.Relative));
                    image_controller_icon.Opacity = enabled_opacity;

                    progressbar_battery.Foreground = new System.Windows.Media.SolidColorBrush(ColorFromHue(batterylevel));
                    progressbar_battery.Value = batterylevel;

                    SetChargingIconActive(isCharging);

                    UpdateBatteryAnim(batterylevel, isCharging);
                }
                else
                {
                    //Not connected for this index, hide it
                    Visibility = Visibility.Collapsed;

                    UpdateBatteryAnim(0, true); //Stop blinking to improve performance
                }
            }
        }

        //Initializes the player color gradients
        private void InitializePlayerColors()
        {
            //Player 01
            color_player_01.StartPoint = new Point(0.5, 0);
            color_player_01.EndPoint = new Point(0.5, 1);
            color_player_01.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00bfff"), 0.0));
            color_player_01.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#007399"), 1.0));

            //Player 02
            color_player_02.StartPoint = new Point(0.5, 0);
            color_player_02.EndPoint = new Point(0.5, 1);
            color_player_02.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#ff0000"), 0.0));
            color_player_02.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#990000"), 1.0));

            //Player 03
            color_player_03.StartPoint = new Point(0.5, 0);
            color_player_03.EndPoint = new Point(0.5, 1);
            color_player_03.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00ff40"), 0.0));
            color_player_03.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#009926"), 1.0));

            //Player 04
            color_player_04.StartPoint = new Point(0.5, 0);
            color_player_04.EndPoint = new Point(0.5, 1);
            color_player_04.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#ff00ff"), 0.0));
            color_player_04.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#990099"), 1.0));

            playerColors = new List<Brush> {color_player_01, color_player_02, color_player_03, color_player_04};
        }

        //Controls blinking animation for low battery
        private void UpdateBatteryAnim(int batterylevel, bool isCharging)
        {
            //is the battery level lower then the threshold
            if (batterylevel < lowBatteryThreshold)
            {
                if (!isCharging)
                {
                    // only begin if not already running
                    if (!isPlayingLowBatAnim)
                    {
                        // true: isControllable so we can stop it later
                        blink_storyboard.Begin(this, true);
                        isPlayingLowBatAnim = true;
                    }
                } else
                {
                    blink_storyboard.Stop(this);
                    isPlayingLowBatAnim = false;
                }
            }
            //Battery level is higher
            else
            {
                blink_storyboard.Stop(this);
                isPlayingLowBatAnim = false;
            }
        }

        //Assigns color to player index label
        private void updatePlayerColor()
        {
            if (self_index >= 1 && self_index <= playerColors.Count)
                count_index.Foreground = playerColors[self_index - 1];
        }

        //Shows or hides the charging icon
        private void SetChargingIconActive(bool charging)
        {
            if (charging)
            {
                icon_charging.Visibility = Visibility.Visible;
            }
            else
            {
                icon_charging.Visibility = Visibility.Hidden;
            }

        }

        //Converts battery level (as hue) to a color
        public static Color ColorFromHue(double hue)
        {
            // Normalize hue to [0, 360)
            hue = hue % 360;
            if (hue < 0) hue += 360;

            //Offset color
            //We don't expect that the hue will ever become higher then 360
            hue = hue * 1.15;

            double s = 1.0; // Full saturation
            double v = 0.9; // Slightly darker

            int hi = (int)(hue / 60) % 6;
            double f = (hue / 60) - Math.Floor(hue / 60);

            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            double r = 0, g = 0, b = 0;

            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }

            return Color.FromRgb(
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255)
            );
        }
    }
}
