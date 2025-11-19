using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WayFinaWebApp.Models;

namespace WayfinaMobileAppBot
{
	public class ZohoIntegration
	{
        private const int maxRetryAttempts = 5;
        private const int delayBetweenRetriesInSeconds = 60;
        private int totalNumberOfCalls = 0;
        private string accessToken = String.Empty;

        public ZohoIntegration()
		{
		}

        public async Task<string?> AddContact(RegistrationModel model)
        {
            var url = $"https://www.zohoapis.com/crm/v3/Contacts";

            var contactData = new
            {
                data = new[]
                {
                    new
                    {
                        Last_Name = model.SecondName ?? "N/A",
                        First_Name = model.FirstName ?? "N/A",
                        Phone = model.ToNormalizedZambia(), 
                        Lead_Source = "Web",
                        Account_Name = new { id = "6819215000000652101" }
                    }
                }
            };

            if(String.IsNullOrEmpty(accessToken))
            {
                accessToken = await GetAccessToken();
            }

            var existsContractId = await GetContactIdByPhoneAsync(model.ToNormalizedZambia());

            if(existsContractId != null)
            {
                return existsContractId;
            }

            int retryCount = 0;

            while (retryCount < maxRetryAttempts)
            {
                try
                {
                    totalNumberOfCalls++;
                    var jsonData = JsonSerializer.Serialize(contactData);
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");
                        HttpResponseMessage response = await client.PostAsync(url, content);

                        // Check for 429 Too Many Requests
                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            retryCount++;
                            Console.WriteLine($"Received 429 Too Many Requests. Retry attempt {retryCount} of {maxRetryAttempts}, Total Calls: {totalNumberOfCalls}");

                            if (retryCount >= maxRetryAttempts)
                            {
                                Console.WriteLine("Max retry attempts reached due to 429 status.");
                                return null;
                            }

                            await Task.Delay(TimeSpan.FromSeconds(delayBetweenRetriesInSeconds));
                            continue; // Retry
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) // 401
                        {
                            accessToken = await GetAccessToken();
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) // 403
                        {
                            accessToken = await GetAccessToken();
                        }

                        response.EnsureSuccessStatusCode();

                        string json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Response received: {json}");

                        using var doc = JsonDocument.Parse(json);

                        if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                        {
                            var details = data[0].GetProperty("details");
                            return details.GetProperty("id").GetString();
                        }
                    }
                    return null;
                }
                catch (HttpRequestException e)
                {
                    retryCount++;
                    Console.WriteLine($"Request failed: {e.Message}. Retry attempt {retryCount} of {maxRetryAttempts}");
                }
            }

            return null;
        }

        public async Task AddDealAsync(string contactId, RegistrationModel model, DateTime createdAt, string dealId = "", string stage = "Qualification")
        {
            var url = "https://www.zohoapis.com/crm/v3/Deals";

            var products = await GetAllProductsAsync();

            int amount = 0;
            var productDetails = new List<object>();

            string isoCreatedAt = createdAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz");

            foreach (var item in model.TypesOfFertilizersEntries
                .Where(x => !string.IsNullOrWhiteSpace(x.TypesOfFertilizersId) && x.TypesOfFertilizersId.Length > 3))
            {
                var product = products.FirstOrDefault(p => p.Code == item.TypesOfFertilizersId);

                if (product != null)
                {
                    productDetails.Add(new
                    {
                        Product_item = new { id = product.Id }, // API expects lowercase `product`
                        Qty = item.Quantity
                    });

                    amount += (int)(product.UnitPrice ?? 0) * item.Quantity;
                }
            }

            var dealData = new
            {
                data = new[]
                {
            new
            {
                Deal_Name = model.FirstName + " " + model.SecondName + " / " + createdAt.Month + "-" + createdAt.Year,
                Amount = amount,
                Stage = stage, // e.g., "Qualification", "Needs Analysis", etc.
                Contact_Name = new { id = contactId }, // Link the deal to a contact
                Account_Name = new { id = "6819215000000652101" },
                Crops = string.Join(", ", model.SelectedCropsGrown),
                Lead_Source = "Web",
                Record_Type = "Integration",
                Farm_Name = model.FarmName,
                Location = model.Location,
                Size_of_Farm = model.SelectedSizeOfFarm,
                Description = model.Details,
                Product_details = productDetails,
                HubSpot_Created_At = isoCreatedAt,
                HubSpot_Id = dealId
            }
        }
            };

            var jsonData = JsonSerializer.Serialize(dealData);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            HttpResponseMessage response = await client.PostAsync(url, content);
            string result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Deal inserted successfully: " + result);
            }
            else
            {
                Console.WriteLine("Failed to insert deal: " + result);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    accessToken = await GetAccessToken();
                    await AddDealAsync(contactId, model, createdAt, stage); // Retry with new token
                }
            }
        }

        public async Task AddDealWithOutContactAsync(RegistrationModel model, DateTime createdAt, string dealId = "", string stage = "Qualification")
        {
            var url = "https://www.zohoapis.com/crm/v3/Deals";

            var products = await GetAllProductsAsync();

            int amount = 0;
            var productDetails = new List<object>();

            string isoCreatedAt = createdAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz");

            foreach (var item in model.TypesOfFertilizersEntries
                .Where(x => !string.IsNullOrWhiteSpace(x.TypesOfFertilizersId) && x.TypesOfFertilizersId.Length > 3))
            {
                var product = products.FirstOrDefault(p => p.Code == item.TypesOfFertilizersId);

                if (product != null)
                {
                    productDetails.Add(new
                    {
                        Product_item = new { id = product.Id }, // API expects lowercase `product`
                        Qty = item.Quantity
                    });

                    amount += (int)(product.UnitPrice ?? 0) * item.Quantity;
                }
            }

            var dealData = new
            {
                data = new[]
                {
            new
            {
                Deal_Name = model.FirstName + " " + model.SecondName + " / " + createdAt.Month + "-" + createdAt.Year,
                Amount = amount,
                Stage = stage, // e.g., "Qualification", "Needs Analysis", etc.
                Account_Name = new { id = "6819215000000652101" },
                Crops = string.Join(", ", model.SelectedCropsGrown),
                Lead_Source = "Web",
                Record_Type = "Integration",
                Farm_Name = model.FarmName,
                Location = model.Location,
                Size_of_Farm = model.SelectedSizeOfFarm,
                Description = model.Details,
                Product_details = productDetails,
                HubSpot_Created_At = isoCreatedAt,
                HubSpot_Id = dealId
            }
        }
            };

            var jsonData = JsonSerializer.Serialize(dealData);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            HttpResponseMessage response = await client.PostAsync(url, content);
            string result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Deal inserted successfully: " + result);
            }
            else
            {
                Console.WriteLine("Failed to insert deal: " + result);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    accessToken = await GetAccessToken();
                    await AddDealWithOutContactAsync(model, createdAt, stage); // Retry with new token
                }
            }
        }

        public async Task<List<ZohoProduct>> GetAllProductsAsync()
        {
            var products = new List<ZohoProduct>();
            var url = $"https://www.zohoapis.com/crm/v3/Products?fields=Product_Name,Product_Code,Unit_Price&page=1&per_page=200";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            HttpResponseMessage response = await client.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        products.Add(new ZohoProduct
                        {
                            Id = item.GetProperty("id").GetString(),
                            Name = item.TryGetProperty("Product_Name", out var nameProp) ? nameProp.GetString() : null,
                            Code = item.TryGetProperty("Product_Code", out var codeProp) ? codeProp.GetString() : null,
                            UnitPrice = item.TryGetProperty("Unit_Price", out var priceProp) && priceProp.ValueKind == JsonValueKind.Number
                                        ? priceProp.GetDecimal()
                                        : 0,
                            QuantityInStock = item.TryGetProperty("Qty_in_Stock", out var qtyProp) && qtyProp.ValueKind == JsonValueKind.Number
                                        ? qtyProp.GetDecimal()
                                        : 0
                        });
                    }
                }

                return products;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return products; // No match found
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                accessToken = await GetAccessToken();
                return await GetAllProductsAsync(); // Retry after refreshing token
            }

            response.EnsureSuccessStatusCode();
            return products;
        }

        public async Task<string?> GetContactIdByPhoneAsync(string phone)
        {
            var url = $"https://www.zohoapis.com/crm/v3/Contacts/search?phone={Uri.EscapeDataString(phone)}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            HttpResponseMessage response = await client.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                {
                    return data[0].GetProperty("id").GetString();
                }

                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return null; // No match found
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                accessToken = await GetAccessToken();
                return await GetContactIdByPhoneAsync(phone); // Retry after refreshing token
            }

            response.EnsureSuccessStatusCode();
            return null;
        }

        private async Task<string> GetAccessToken()
        {
            var url = "https://accounts.zoho.com/oauth/v2/token?refresh_token=1000.9981dd58a63663260441338c18e13d96.1ad02511750ba2838f982f800e87ed32&client_id=1000.MGX1F2N2IIYAS782G48FHHSSIEX7CU&client_secret=f8e382444ef089aaadc8cd28b89e6933728673f6d7&redirect_uri=http://www.zoho.com/books&grant_type=refresh_token";

            try
            {
                var tokenRequest = new
                {

                };

                using (var client = new HttpClient())
                {
                    var response = await client.PostAsJsonAsync(url, tokenRequest);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Request successful: {json}");

                        var accessTokenResponse = JsonSerializer.Deserialize<AccessTokenResponse>(json);
                        return accessTokenResponse != null ? accessTokenResponse.access_token : String.Empty;
                    }
                    else
                    {
                        Console.WriteLine($"Request failed: {response.StatusCode}");
                        return String.Empty;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Exception occurred while sending request: {ex.Message}");
                return String.Empty;
            }
        }

        public async Task<List<ZohoDeal>> GetAllDealsAsync()
        {
            var allDeals = new List<ZohoDeal>();
            int page = 1;
            const int perPage = 200; // max page size

            while (true)
            {
                using (var client = new HttpClient())
                {
                    var url = $"https://www.zohoapis.com/crm/v3/Deals?page={page}&per_page={perPage}&fields=id,HubSpot_Id";
                    var response = await client.GetAsync(url);

                    // Handle token expiry
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        accessToken = await GetAccessToken();
                        client.DefaultRequestHeaders.Remove("Authorization");
                        client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");
                        response = await client.GetAsync(url);
                    }

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();

                    var result = JsonSerializer.Deserialize<ZohoDealsResponse>(json);
                    if (result?.Data == null || result.Data.Length == 0)
                        break;

                    allDeals.AddRange(result.Data);

                    // if we got less than a full page, we’re done
                    if (result.Data.Length < perPage)
                        break;

                    page++;
                }
            }

            return allDeals;
        }

        public async Task<bool> DeleteDealAsync(string dealId)
        {
            var url = $"https://www.zohoapis.com/crm/v3/Deals/{dealId}";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            HttpResponseMessage response = await client.DeleteAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                // Zoho returns an array under "data" with a status per record
                if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                {
                    var status = data[0].GetProperty("status").GetString();
                    return string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                // NoContent could mean nothing to delete
                return false;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // refresh token and retry once
                accessToken = await GetAccessToken();
                return await DeleteDealAsync(dealId);
            }

            // For any other unexpected status, throw
            response.EnsureSuccessStatusCode();
            return false;
        }

        public async Task<string?> AddLeadAsync(string contactId, RegistrationModel model, DateTime createdAt, string dealId = "")
        {
            var url = "https://www.zohoapis.com/crm/v3/Leads";

            var products = await GetAllProductsAsync();

            int amount = 0;
            var productDetails = new List<object>();

            string isoCreatedAt = createdAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz");

            foreach (var item in model.TypesOfFertilizersEntries
                .Where(x => !string.IsNullOrWhiteSpace(x.TypesOfFertilizersId) && x.TypesOfFertilizersId.Length > 3))
            {
                var product = products.FirstOrDefault(p => p.Code == item.TypesOfFertilizersId);

                if (product != null)
                {
                    productDetails.Add(new
                    {
                        Item = new { id = product.Id }, // API expects lowercase `product`
                        Qty = item.Quantity
                    });

                    amount += (int)(product.UnitPrice ?? 0) * item.Quantity;
                }
            }

            var dealData = new
            {
                data = new[]
                {
            new
            {
                First_Name = model.FirstName ?? "N/A",
                Last_Name = model.SecondName ?? "N/A",
                Contact = new { id = contactId }, // Link the deal to a contact
                Crops_Grown = string.Join(", ", model.SelectedCropsGrown),
                Farm_Name = model.FarmName,
                Size_of_Farm_hectares = model.SelectedSizeOfFarm,
                Description = model.Details,
                Product_Data = productDetails,
                //Mobile = model.Phone,
                Location = model.Location,
                Lead_Status = "New Inquiry",
                //Street = model.Location,
                Country = "Zambia",
                Lead_Source = "Web",
                Phone = model.Phone
            }
        }
            };

            var jsonData = JsonSerializer.Serialize(dealData);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            HttpResponseMessage response = await client.PostAsync(url, content);
            string result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Lead inserted successfully: " + result);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var createResponse = JsonSerializer.Deserialize<ZohoCreateResponse>(result, options);

                var leadId = createResponse?
                    .data?
                    .FirstOrDefault()?
                    .details?
                    .id;

                return leadId;
            }
            else
            {
                Console.WriteLine("Failed to insert lead: " + result);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    accessToken = await GetAccessToken();
                    await AddLeadAsync(contactId, model, createdAt); // Retry with new token
                }

                return null;
            }
        }

        public async Task AttachToLeadAsync(
            string leadId,
            byte[] imageBytes,
            string fileName)
        {
            if(String.IsNullOrEmpty(accessToken))
            {
                accessToken = await GetAccessToken();
            }

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            using var content = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(imageBytes);
            // adjust if it's PNG, PDF, etc.
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

            // "file" is the required field name for Zoho CRM attachments
            content.Add(fileContent, "file", fileName);

            var url = $"https://www.zohoapis.com/crm/v3/Leads/{leadId}/Attachments";
            using var response = await client.PostAsync(url, content);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Zoho error {response.StatusCode}: {body}");
            }

            Console.WriteLine("Attachment uploaded OK: " + body);
        }

        public async Task AttachToContactAsync(
            string contactId,
            byte[] imageBytes,
            string fileName)
        {
            if(String.IsNullOrEmpty(accessToken))
            {
                accessToken = await GetAccessToken();
            }

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {accessToken}");

            using var content = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(imageBytes);
            // adjust if it's PNG, PDF, etc.
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

            // "file" is the required field name for Zoho CRM attachments
            content.Add(fileContent, "file", fileName);

            var url = $"https://www.zohoapis.com/crm/v3/Contacts/{contactId}/Attachments";
            using var response = await client.PostAsync(url, content);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Zoho error {response.StatusCode}: {body}");
            }

            Console.WriteLine("Attachment uploaded OK: " + body);
        }
    }
}