using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace WayFinaWebApp.Models
{
    public class RegistrationModel
    {
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Second Name")]
        public string SecondName { get; set; }

        [Required]
        [Display(Name = "Farm Name")]
        [MinLength(3, ErrorMessage = "Farm Name must be at least 3 characters.")]
        public string FarmName { get; set; }

        public List<ProductEntry> TypesOfFertilizersEntries { get; set; } = new List<ProductEntry>();

        [Required]
        [Display(Name = "Location")]
        [MinLength(3, ErrorMessage = "Location must be at least 3 characters.")]
        public string Location { get; set; }

        [Required]
        [Display(Name = "Crops grown")]
        public List<string> SelectedCropsGrown { get; set; }

        [Required]
        [Display(Name = "Size of Farm, (hectares)")]
        public string SelectedSizeOfFarm { get; set; }

        [Required]
        [Display(Name = "Phone")]
        [RegularExpression(@"^\+260\s\d{3}\s\d{3}\s\d{3}$",
        ErrorMessage = "Please enter a valid Zambian phone number")]
        public string Phone { get; set; }

        [Display(Name = "Details")]
        public string Details { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string ToNormalizedZambia()
        {
            var digits = Regex.Replace(this.Phone, @"\D", "");
            if (digits.StartsWith("260")) digits = digits.Substring(3);
            digits = digits.PadLeft(9, '0'); // left-pad if short
            return $"+260 {digits.Substring(0,3)} {digits.Substring(3,3)} {digits.Substring(6,3)}";
        }
    }

    public class ProductEntry
    {
        public string TypesOfFertilizersId { get; set; }

        public int Quantity { get; set; }
    }

    public class Lead
    {
        public Lead(Guid id, string fullName, string farmName, string typesOfFertilizers, int quantity,
                string location, string sizeOfFarm, string phone, string cropsGrown, string details, DateTime createdAt,
                string source = "Web")
        {
            Id = id;
            FullName = fullName;
            FarmName = farmName;
            TypesOfFertilizers = typesOfFertilizers;
            Quantity = quantity;
            Location = location;
            SizeOfFarm = sizeOfFarm;
            Phone = phone;
            Details = details;
            CropsGrown = cropsGrown;
            Source = source;
            CreatedAt = createdAt;
        }

        public Guid Id { get; set; }

        public string Source { get; set; }

        public DateTime CreatedAt { get; set; }

        public string FullName { get; set; }

        public string FarmName { get; set; }

        public string TypesOfFertilizers { get; set; }

        public int Quantity { get; set; }

        public string Location { get; set; }

        public string SizeOfFarm { get; set; }

        public string Phone { get; set; }

        public string CropsGrown { get; set; }

        public string Details { get; set; }

        public override string ToString()
        {
            return $"Source: {Source}, " +
                   $"CreatedAt: {CreatedAt}, " +
                   $"FullName: {FullName}, " +
                   $"FarmName: {FarmName}, " +
                   $"TypesOfFertilizers: {TypesOfFertilizers}, " +
                   $"Quantity: {Quantity}, " +
                   $"Location: {Location}, " +
                   $"SizeOfFarm: {SizeOfFarm}, " +
                   $"Phone: {Phone}, " +
                   $"CropsGrown: {CropsGrown}, " +
                   $"Details: {Details} ";
        }
    }
}

