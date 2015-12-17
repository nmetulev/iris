using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DTOLib
{
    
    public class IrisMessage
    {
        public string Success { get; set; }
        public int DoorID { get; set; }
        public double Probability { get; set; }
        public double Anger { get; set; }
        public double Contempt { get; set; }
        public double Disgust { get; set; }
        public double Fear { get; set; }
        public double Happiness { get; set; }
        public double Neutral { get; set; }
        public double Sadness { get; set; }
        public double Surprise { get; set; }
        public double Temperature { get; set; }
        public double Noise { get; set; }
        public double Brightness { get; set; }
        public double Humidity { get; set; }

        public IrisMessage(
            string success = "no", 
            double prob = 0.0,
            int doorid=0,
            double anger = 0.0,
            double contempt = 0.0,
            double disgust = 0.0,
            double fear = 0.0,
            double happiness = 0.0,
            double neutral = 0.0,
            double sadness = 0.0,
            double surprise = 0.0,
            double temperature = 0.0,
            double noise = 0.0,
            double brightness = 0.0,
            double humidity = 0.0
            )
        {
            this.Success = success;
            this.DoorID = doorid;
            this.Probability = prob;
            this.Anger = anger;
            this.Contempt = contempt;
            this.Disgust = disgust;
            this.Fear = fear;
            this.Happiness = happiness;
            this.Neutral = neutral;
            this.Sadness = sadness;
            this.Surprise = surprise;
            this.Temperature = temperature;
            this.Noise = noise;
            this.Brightness = brightness;
            this.Humidity = humidity;
        }
    }
}
