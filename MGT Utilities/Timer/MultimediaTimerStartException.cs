using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MGT.Utilities.Timer
{
    /// <summary>
    /// The exception that is thrown when a timer fails to start.
    /// </summary>
    public class MultimediaTimerStartException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the TimerStartException class.
        /// </summary>
        /// <param name="message">
        /// The error message that explains the reason for the exception. 
        /// </param>
        public MultimediaTimerStartException(string message)
            : base(message)
        {
        }
    }
}
