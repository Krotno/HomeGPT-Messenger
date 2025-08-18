using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeGPT_Messenger.Models
{
    public enum MessageStatus { Sent, WaitingForResponse, Error, Done }
    public class Message
    {
        public string Sender { get; set; } = "user";// "User" and "LLM"
        public string Text { get; set; } = "";//info
        public string? ImagePath { get; set; }
        public DateTime Timestamp { get; set; }//Time message
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
    }
}
