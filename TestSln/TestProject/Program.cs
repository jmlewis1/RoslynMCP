using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using TestProject.NS;

namespace TestProject
{
    /// <summary>
    /// Represents a person with basic contact information.
    /// Used for testing JSON serialization and deserialization functionality.
    /// </summary>
    public class Person
    {
        /// <summary>
        /// Gets or sets the full name of the person.
        /// </summary>
        /// <value>The person's name as a string. Defaults to empty string.</value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the age of the person in years.
        /// </summary>
        /// <value>The person's age as an integer.</value>
        public int Age { get; set; }

        /// <summary>
        /// Gets or sets the email address of the person.
        /// </summary>
        /// <value>The person's email address as a string. Defaults to empty string.</value>
        public string Email { get; set; } = string.Empty;

        public int aField = 0;
    }

    /// <summary>
    /// Main program class that demonstrates various functionality including 
    /// JSON serialization/deserialization and HTTP client usage with dependency injection.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The main entry point of the application.
        /// Demonstrates JSON serialization, HTTP client usage, and dependency injection.
        /// </summary>
        /// <param name="args">Command line arguments (not used in this application).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        static async Task Main(string[] args)
        {
            Console.WriteLine("Simple C# Test Application with NuGet packages");
            Console.WriteLine("==============================================");

            // Test Newtonsoft.Json serialization
            var person = new Person 
            { 
                Name = "John Doe", 
                Age = 30, 
                Email = "john@example.com" 
            };

            string json = JsonConvert.SerializeObject(person, Formatting.Indented);
            Console.WriteLine("JSON Serialization:");
            Console.WriteLine(json);

            var deserializedPerson = JsonConvert.DeserializeObject<Person>(json);
            Console.WriteLine($"\nDeserialized: {deserializedPerson?.Name}, Age: {deserializedPerson?.Age}");

            // Test HTTP client with dependency injection
            var services = new ServiceCollection();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();

            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();

            try
            {
                Console.WriteLine("\nTesting HTTP client...");
                var response = await httpClient.GetStringAsync("https://httpbin.org/json");
                var jsonResponse = JsonConvert.DeserializeObject(response);
                Console.WriteLine("HTTP Response received and parsed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP request failed: {ex.Message}");
            }

            Generic<Person> generic = new Generic<Person>();
            Console.WriteLine("\nApplication completed successfully!");

        }

        static void TestFunc(Person person)
        {
            Console.WriteLine(person.Name);
        }
    }
}
