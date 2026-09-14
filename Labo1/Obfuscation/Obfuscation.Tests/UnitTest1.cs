using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Obfuscation.Tests;

[Collection("ConsoleTests")]
public class FriendlyTests
{
    private static readonly object ConsoleLock = new object();

    [Fact]
    public void Constructor_SetsNamePropertyCorrectly()
    {
        // Arrange
        const string expectedName = "Alice";

        // Act
        var friend = new Friendly(expectedName);

        // Assert
        Assert.Equal(expectedName, friend.Name);
    }

    [Theory]
    [InlineData("Alice")]
    [InlineData("Bob")]
    [InlineData("Charlie")]
    public void SayHello_PrintsExpectedGreetingToConsole(string name)
    {
        lock (ConsoleLock)
        {
            // Arrange
            var friend = new Friendly(name);
            using var stringWriter = new StringWriter();
            var originalOut = Console.Out;

            try
            {
                Console.SetOut(stringWriter);

                // Act
                friend.SayHello();

                // Assert
                var expectedOutput = $"Hello, my name is {name}{Environment.NewLine}";
                Assert.Equal(expectedOutput, stringWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }

    [Theory]
    [InlineData("Alice", "Bob")]
    [InlineData("Bob", "Alice")]
    [InlineData("Charlie", "David")]
    public void SayGoodbye_PrintsExpectedFarewellToConsole(string senderName, string receiverName)
    {
        lock (ConsoleLock)
        {
            // Arrange
            var friend = new Friendly(senderName);
            using var stringWriter = new StringWriter();
            var originalOut = Console.Out;

            try
            {
                Console.SetOut(stringWriter);

                // Act
                friend.SayGoodbye(receiverName);

                // Assert
                var expectedOutput = $"Goodbye {receiverName}{Environment.NewLine}";
                Assert.Equal(expectedOutput, stringWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}

[Collection("ConsoleTests")]
public class ProgramTests
{
    private static readonly object ConsoleLock = new object();

    [Fact]
    public void Main_ExecutesFullConversationSuccessfully()
    {
        lock (ConsoleLock)
        {
            // Arrange
            var mainMethod = typeof(Friendly).Assembly
                .GetType("Obfuscation.Program")?
                .GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(mainMethod);

            using var stringWriter = new StringWriter();
            var originalOut = Console.Out;

            try
            {
                Console.SetOut(stringWriter);

                // Act
                mainMethod.Invoke(null, null);

                // Assert
                var output = stringWriter.ToString();
                Assert.Contains($"Hello, my name is Alice{Environment.NewLine}", output);
                Assert.Contains($"Hello, my name is Bob{Environment.NewLine}", output);
                Assert.Contains($"Goodbye Bob{Environment.NewLine}", output);
                Assert.Contains($"Goodbye Alice{Environment.NewLine}", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }

    [Fact]
    public void Converse_ExecutesConversationBetweenGivenNames()
    {
        lock (ConsoleLock)
        {
            // Arrange
            var converseMethod = typeof(Friendly).Assembly
                .GetType("Obfuscation.Program")?
                .GetMethod("Converse", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            Assert.NotNull(converseMethod);

            using var stringWriter = new StringWriter();
            var originalOut = Console.Out;

            try
            {
                Console.SetOut(stringWriter);

                // Act
                converseMethod.Invoke(null, new object[] { "David", "Emma" });

                // Assert
                var output = stringWriter.ToString();
                Assert.Contains($"Hello, my name is David{Environment.NewLine}", output);
                Assert.Contains($"Hello, my name is Emma{Environment.NewLine}", output);
                Assert.Contains($"Goodbye Emma{Environment.NewLine}", output);
                Assert.Contains($"Goodbye David{Environment.NewLine}", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
