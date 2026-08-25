using System.Net;
using System.Text;
using System.Text.Json;
using TechCurse.Api.IntegrationTests.Fixtures;
using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using Xunit;

namespace TechCurse.Api.IntegrationTests.Contracts;

[Trait("Category", "Integration")]
public class PaymentsPagedResponseTests
{
    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler, Uri? baseAddress = null)
    {
        var messageHandler = new StubHttpMessageHandler(handler);
        var client = new HttpClient(messageHandler);
        if (baseAddress != null)
            client.BaseAddress = baseAddress;
        return client;
    }

    [Fact(DisplayName = "GET /payments should return list of payments")]
    public async Task ListPayments_ReturnsOkAndPayments()
    {
        // Arrange
        IEnumerable<Payment> payments = new List<Payment>()
        {
            new Payment() { PaymentId = 1, EnrollmentId = 1, StudentId = 1, Amount = 100.50m, Status = PaymentStatus.Paid, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-5), PaidAt = null, ExternalTransactionId = "PAID_123124123321" },
            new Payment() { PaymentId = 2, EnrollmentId = 2, StudentId = 2, Amount = 250.75m, Status = PaymentStatus.Pending, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-3), PaidAt = null, ExternalTransactionId = "PAID_123124123321" },
            new Payment() { PaymentId = 3, EnrollmentId = 3, StudentId = 3, Amount = 75.00m, Status = PaymentStatus.Failed, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-1), PaidAt = null, ExternalTransactionId = "PAID_123124123321" }
        };

        var paymentsOutputDto = payments.Select(p => new PaymentOutputDto(
            p.PaymentId,
            p.EnrollmentId,
            p.StudentId,
            p.Amount,
            p.Status,
            p.IsActive,
            p.CreatedAt,
            p.PaidAt,
            p.ExternalTransactionId
        )).ToList();

        var responseObject = new PagedResultDto<PaymentOutputDto>(
            paymentsOutputDto,
            paymentsOutputDto.Count,
            1,
            10
        );

        var handler = new Func<HttpRequestMessage, HttpResponseMessage>(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.PathAndQuery == "/tech-curse/Payment")
            {
                var json = JsonSerializer.Serialize(responseObject);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = CreateClient(handler, new Uri("http://localhost/"));

        //Act
        var response = await httpClient.GetAsync("/tech-curse/Payment");

        //Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contentString = await response.Content.ReadAsStringAsync();
        var pagedResponse = JsonSerializer.Deserialize<PagedResultDto<PaymentOutputDto>>(
            contentString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(pagedResponse);
        Assert.Equal(paymentsOutputDto.Count, pagedResponse!.TotalCount);

        var responseContent = pagedResponse.Items.ToList();

        for (int i = 0; i < paymentsOutputDto.Count; i++)
        {
            Assert.Equal(paymentsOutputDto[i].PaymentId, responseContent[i].PaymentId);
            Assert.Equal(paymentsOutputDto[i].EnrollmentId, responseContent[i].EnrollmentId);
            Assert.Equal(paymentsOutputDto[i].StudentId, responseContent[i].StudentId);
            Assert.Equal(paymentsOutputDto[i].Amount, responseContent[i].Amount);
            Assert.Equal(paymentsOutputDto[i].Status, responseContent[i].Status);
            Assert.Equal(paymentsOutputDto[i].IsActive, responseContent[i].IsActive);
            Assert.Equal(paymentsOutputDto[i].CreatedAt, responseContent[i].CreatedAt);
            Assert.Equal(paymentsOutputDto[i].PaidAt, responseContent[i].PaidAt);
            Assert.Equal(paymentsOutputDto[i].ExternalTransactionId, responseContent[i].ExternalTransactionId);
        }

        Assert.Equal(1, pagedResponse.PageNumber);
        Assert.Equal(10, pagedResponse.PageSize);
        Assert.Equal(1, pagedResponse.TotalPages);
    }
}
