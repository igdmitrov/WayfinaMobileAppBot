using System.Text.RegularExpressions;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using WayFinaWebApp.Models;

namespace WayfinaMobileAppBot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IOptions<ZohoAppOptions> _optsZoho;

    public Worker(ILogger<Worker> logger, IHostApplicationLifetime lifetime, IOptions<ZohoAppOptions> optsZoho)
    {
        _logger = logger;
        _lifetime = lifetime;
        _optsZoho = optsZoho;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Environment.SetEnvironmentVariable(
            "GOOGLE_APPLICATION_CREDENTIALS",
            Path.Combine(AppContext.BaseDirectory, "firebase-key.json")
        );

        string projectId = "wayfine-ef146";
        FirestoreDb db = FirestoreDb.Create(projectId);

        CollectionReference requests = db.Collection("requests");
        Query pendingQuery = requests.WhereEqualTo("status", "pending");

        QuerySnapshot pendingSnapshot = await pendingQuery.GetSnapshotAsync();

        Console.WriteLine($"Found {pendingSnapshot.Count} pending requests.");

        foreach (DocumentSnapshot doc in pendingSnapshot.Documents)
        {
            var data = doc.ToDictionary();

            string id = doc.Id;
            string userId = data.ContainsKey("userId") ? data["userId"]?.ToString() ?? "" : "";

            Console.WriteLine($"DocId: {id}, userId: {userId}");

            DocumentReference docRef = db.Collection("users").Document(userId);
            DocumentSnapshot userData = await docRef.GetSnapshotAsync();

            string firstName = userData.ContainsField("firstName")
                ? userData.GetValue<string>("firstName")
                : "";

            string secondName = userData.ContainsField("lastName")
                ? userData.GetValue<string>("lastName")
                : "";

            string phone = userData.ContainsField("phoneNumber")
                ? userData.GetValue<string>("phoneNumber")
                : "";

            string idPhoto = userData.ContainsField("idPhoto")
                ? userData.GetValue<string>("idPhoto")
                : "";

            string idPhotoBackSide = userData.ContainsField("idPhotoBackSide")
                ? userData.GetValue<string>("idPhotoBackSide")
                : "";

            string selfiePhoto = userData.ContainsField("selfiePhoto")
                ? userData.GetValue<string>("selfiePhoto")
                : "";

            string farmSize = data.ContainsKey("farmSize") ? data["farmSize"]?.ToString() ?? "" : "";

            GeoPoint location = data.ContainsKey("location") && data["location"] is GeoPoint gp
                    ? gp
                    : default;

            Console.WriteLine($"Location: ({location.Latitude}, {location.Longitude})");

            var dto = new RegistrationModel();
            dto.FirstName = firstName;
            dto.SecondName = secondName;
            dto.Phone = phone;
            dto.SelectedSizeOfFarm = farmSize;
            dto.Latitude = location.Latitude;
            dto.Longitude = location.Longitude;

            List<ProductEntry> products = new List<ProductEntry>();

            if (data.TryGetValue("requestFertilizers", out var fertValue) &&
                fertValue is IEnumerable<object> fertArray)
            {
                foreach (var item in fertArray)
                {
                    // Each item is a map (Dictionary<string, object>)
                    var fert = (Dictionary<string, object>)item;

                    var fertilizerId = fert.ContainsKey("fertilizer")
                        ? fert["fertilizer"]?.ToString()
                        : "";

                    var quantity = fert.ContainsKey("quantity")
                        ? Convert.ToInt32(fert["quantity"])
                        : 0;

                    Console.WriteLine($"fertId={fertilizerId}, qty={quantity}");

                    products.Add(new ProductEntry() { TypesOfFertilizersId = fertilizerId, Quantity = quantity });
                }
            }

            var crops = new List<string>();
            if (doc.ContainsField("cropsGrown"))
            {
                crops = doc.GetValue<List<string>>("cropsGrown");
                Console.WriteLine("cropsGrown: " + string.Join(", ", crops));
            }

            dto.TypesOfFertilizersEntries = products;
            dto.SelectedCropsGrown = crops;

            try
            {
                var zohoIntegration = new ZohoIntegration(_optsZoho.Value);
                var contactRet = zohoIntegration.AddContact(dto).Result;
                var leadId = await zohoIntegration.AddLeadAsync(contactRet.Item1, dto, DateTime.UtcNow);

                if (contactRet.Item2 == true)
                {
                    if (String.IsNullOrEmpty(idPhoto) == false)
                    {
                        var photoFile = await DownloadImageAsync(idPhoto);
                        await zohoIntegration.AttachToContactAsync(contactRet.Item1, photoFile, "ID_Front.jpg");
                    }

                    if (String.IsNullOrEmpty(idPhotoBackSide) == false)
                    {
                        var photoBackSideFile = await DownloadImageAsync(idPhotoBackSide);
                        await zohoIntegration.AttachToContactAsync(contactRet.Item1, photoBackSideFile, "ID_Back.jpg");
                    }

                    if (String.IsNullOrEmpty(selfiePhoto) == false)
                    {
                        var selfieFile = await DownloadImageAsync(selfiePhoto);
                        await zohoIntegration.AttachToContactAsync(contactRet.Item1, selfieFile, "Selfie.jpg");
                    }
                }

                var msg =
           $@"<b>🆕 New Registration (MobileApp)</b>

            <b>👤 Farmer</b>
            • <b>First name:</b> {dto.FirstName}
            • <b>Second name:</b> {dto.SecondName}
            • <b>Phone:</b> <code>{dto.ToNormalizedZambia()}</code>

            <b>🚜 Farm</b>
            • <b>Size (ha):</b> {dto.SelectedSizeOfFarm}
            • <b>Location:</b> {dto.Location}

            <b>🌱 Crops grown</b>
            {(dto.SelectedCropsGrown != null && dto.SelectedCropsGrown.Any()
               ? string.Join("\n", dto.SelectedCropsGrown)
               : "<i>Not provided</i>")}

            <b>🧪 Fertilizers</b>
            {(products != null && products.Any()
               ? string.Join("\n", products.Select(p => $"{p.TypesOfFertilizersId} x{p.Quantity}"))
               : "<i>Not provided</i>")}

            <b>📝 Details</b>
            {(string.IsNullOrWhiteSpace(dto.Details)
               ? "<i>Not provided</i>"
               : dto.Details)}";

                TelegramNotifier.SendHtmlAsync(msg).Wait();
            }
            catch (Exception ex)
            {
                TelegramNotifier.SendHtmlAsync($"Zoho CRM from MobileApp: {ex.Message}").Wait();
                Console.WriteLine($"Error integrating with Zoho: {ex.Message}");
            }

            await doc.Reference.UpdateAsync(new Dictionary<string, object>
            {
                { "status", "inProgress" }
            });
        }

        _lifetime.StopApplication();
    }

    public async Task<byte[]> DownloadImageAsync(string url)
    {
        using var http = new HttpClient();

        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        byte[] imageBytes = await http.GetByteArrayAsync(url);
        return imageBytes;
    }
}
