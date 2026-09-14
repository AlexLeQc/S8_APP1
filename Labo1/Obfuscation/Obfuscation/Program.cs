using System;

namespace Obfuscation
{
    static class Program
    {
        static void Main()
        {
            Converse("Alice", "Bob");
        }

        private static void Converse(string name1, string name2)
        {
            Friendly friend1 = new Friendly(name1);
            Friendly friend2 = new Friendly(name2);

            friend1.SayHello();
            friend2.SayHello();

            friend1.SayGoodbye(friend2.Name);
            friend2.SayGoodbye(friend1.Name);
        }
    }

    class Friendly
    {
        private const string Greeting = "Hello";

        public string Name { get; }

        public Friendly(string name)
        {
            this.Name = name;
        }

        public void SayHello()
        {
            Console.WriteLine($"{Greeting}, my name is {this.Name}");
        }

        public void SayGoodbye(string otherName)
        {
            Console.WriteLine($"Goodbye {otherName}");
        }
    }
}