using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DTOLib
{
    public class IrisMessage
    {
        public bool Success { get; set; }
        public double Probability { get; set; }

        public IrisMessage (bool success, double prob)
        {
            this.Success = success;
            this.Probability = prob;
        }
    }
}
