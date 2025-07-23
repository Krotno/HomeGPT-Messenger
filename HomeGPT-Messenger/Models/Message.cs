using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeGPT_Messenger.Models
{
    public class Message
    {
        public string Sender { get; set; }// "User" and "LLM"
        public string Text { get; set; }//info
        public DateTime Timestamp { get; set; }//Time message
    }
}
