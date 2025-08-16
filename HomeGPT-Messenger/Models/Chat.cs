using System;
using System.Collections.Generic;

namespace HomeGPT_Messenger.Models
{
    public class Chat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public List<Message> Messages { get; set; } = new List<Message>();
        public bool IsFrozen { get; set; }
        public string? ModelName { get; set; }//Запоминаем модель LLM в чате
        public string? SystemPromt { get; set; } = "";
    }
}
