using GrindCore.Tests;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ReflectionExample
{
    class Program
    {
        static void Main(string[] args)
        {
            // Reference the assembly containing the test classes
            Assembly assembly = typeof(HashTests).Assembly;

            // Get all types in the assembly
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                // Check if the type is a test class (contains methods with [Theory] attribute)
                var theoryMethods = type.GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(TheoryAttribute), true).Any());

                if (theoryMethods.Any())
                {
                    // Create an instance of the test class
                    object testClassInstance = Activator.CreateInstance(type);

                    Console.WriteLine($"Class: {type.Name}");

                    foreach (var method in theoryMethods)
                    {
                        // Get the InlineData attributes for the method
                        var inlineDataAttributes = method.GetCustomAttributes(typeof(InlineDataAttribute), true)
                            .Cast<InlineDataAttribute>();

                        foreach (var inlineData in inlineDataAttributes)
                        {
                            // Get the parameters for the method
                            var parameters = inlineData.GetData(null).First();

                            // Invoke the method with the parameters
                            try
                            {
                                method.Invoke(testClassInstance, parameters);
                                Console.WriteLine($"Pass: {method.Name}({string.Join(", ", parameters)})");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Fail: {method.Name}({string.Join(", ", parameters)})");
                                PrintExceptionDetails(ex);
                            }
                        }
                    }
                }
            }
        }

        static void PrintExceptionDetails(Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Console.WriteLine("Inner Exception:");
                PrintExceptionDetails(ex.InnerException);
            }
        }
    }
}