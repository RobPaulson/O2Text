using System;
using O2TextLib;

namespace ConsoleDemo
{
    class Program
    {
        static void Main()
        {
            // To begin instantiate an instance of O2Text 
            var o2 = new O2Text("08666587", "yourpassword");

            // Use SendTextMessage (or SendTextMessageAsync) to send a message.
            var result = o2.SendTextMessage("08666587", "Your message here");
            Console.WriteLine("Message sent!  You have {0} messages remaining", result);

            Console.Read();
        }
    }
}
