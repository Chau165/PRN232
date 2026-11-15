using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Policies;
using PRN232.Application.Services;
using PRN232.Infrastructure.Data;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;
namespace PRN232.Application.Tests;
public class PropertyCoordinateApiTests
{
    [Theory]
    [InlineData(null,null)][InlineData("10.85",null)][InlineData(null,"106.77")]
    [InlineData("0","0")][InlineData("91","106")][InlineData("10","181")]
    public void Missing_or_invalid_coordinate_pair_is_rejected(string? lat,string? lon)
    {
        decimal? Parse(string? s)=>s is null?null:decimal.Parse(s,System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<PropertyValidationException>(()=>PropertyCoordinatePolicy.Require(Parse(lat),Parse(lon)));
    }
    [Fact] public void Coordinates_are_rounded_to_six_places()
    {Assert.Equal(new AddressCoordinates(10.851235m,106.771235m),PropertyCoordinatePolicy.Require(10.8512345m,106.7712345m));}
    private static MultipartFormDataContent Form(string address="121 Lê Văn Việt",string? lat="10.85",string? lon="106.77",string? province="79",string? ward="26758")
    {
        var f=new MultipartFormDataContent();f.Add(new StringContent("Nhà trọ ghim bản đồ"),"propertyName");f.Add(new StringContent(address),"addressText");
        if(lat is not null)f.Add(new StringContent(lat),"latitude");if(lon is not null)f.Add(new StringContent(lon),"longitude");
        if(province is not null)f.Add(new StringContent(province),"provinceCode");if(ward is not null)f.Add(new StringContent(ward),"wardCode");return f;
    }
    [Theory]
    [InlineData(null,401)][InlineData("customer@example.test",403)][InlineData("manager@example.test",403)]
    public async Task Only_admin_can_write_coordinates(string? email,int status)
    {
        using var factory=new BookingsApiFactory();using var client=factory.CreateClient();if(email is not null)await LoginAsync(client,email);
        using var form=Form();Assert.Equal((HttpStatusCode)status,(await client.PostAsync("/api/properties",form)).StatusCode);
    }
    [Theory]
    [InlineData(null,null)][InlineData("10.85",null)][InlineData("0","0")]
    [InlineData("NaN","106.77")][InlineData("10.85","Infinity")]
    public async Task Api_rejects_missing_partial_or_malformed_coordinates(string? lat,string? lon)
    {
        using var factory=new BookingsApiFactory();using var client=factory.CreateClient();await LoginAsync(client,"admin@example.test");
        using var form=Form(lat:lat,lon:lon);Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/properties",form)).StatusCode);
    }
    [Fact]
    public async Task Create_and_pin_update_store_exact_coordinates_and_address_changes_require_new_pair()
    {
        using var factory=new BookingsApiFactory();using var client=factory.CreateClient();await LoginAsync(client,"admin@example.test");
        using var form=Form(lat:"10.8512345",lon:"106.7712345");var created=await client.PostAsync("/api/properties",form);
        Assert.True(created.StatusCode==HttpStatusCode.Created,await created.Content.ReadAsStringAsync());var dto=(await created.Content.ReadFromJsonAsync<PropertyDto>())!;
        Assert.Equal(10.851235m,dto.Latitude);Assert.Equal(106.771235m,dto.Longitude);
        using var keep=Form(lat:null,lon:null);Assert.Equal(HttpStatusCode.OK,(await client.PutAsync($"/api/properties/{dto.PropertyId}",keep)).StatusCode);
        using var moved=Form(lat:"10.86",lon:"106.78",province:null,ward:null);var updated=await client.PutAsync($"/api/properties/{dto.PropertyId}",moved);
        Assert.Equal(HttpStatusCode.OK,updated.StatusCode);var pin=(await updated.Content.ReadFromJsonAsync<PropertyDto>())!;
        Assert.Equal(10.86m,pin.Latitude);Assert.Equal(dto.AddressText,pin.AddressText);
        using var invalid=Form(address:"122 Lê Văn Việt",lat:null,lon:null);Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsync($"/api/properties/{dto.PropertyId}",invalid)).StatusCode);
        using var codeChanged=Form(lat:null,lon:null,ward:"26743");Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsync($"/api/properties/{dto.PropertyId}",codeChanged)).StatusCode);
        using var changed=Form(address:"122 Lê Văn Việt",lat:"10.87",lon:"106.79");Assert.Equal(HttpStatusCode.OK,(await client.PutAsync($"/api/properties/{dto.PropertyId}",changed)).StatusCode);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var row=await db.Properties.SingleAsync(x=>x.PropertyId==dto.PropertyId);
        Assert.Equal("122 Lê Văn Việt",row.AddressText);Assert.Equal(10.87m,row.Latitude);Assert.Equal(106.79m,row.Longitude);
    }
    [Fact]
    public async Task Invalid_administrative_codes_and_removed_confirmation_endpoint_are_rejected()
    {
        using var factory=new BookingsApiFactory();using var client=factory.CreateClient();await LoginAsync(client,"admin@example.test");
        using var form=Form(ward:"00004");Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/properties",form)).StatusCode);
        Assert.Contains((await client.PostAsJsonAsync("/api/properties/location-confirmation",new {})).StatusCode,new[] {HttpStatusCode.NotFound,HttpStatusCode.MethodNotAllowed});
    }
    [Fact] public void Model_matches_existing_migrations_without_confirmation_metadata()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.False(db.Database.HasPendingModelChanges());Assert.DoesNotContain(db.Database.GetMigrations(),x=>x.EndsWith("_AddPropertyLocationConfirmation"));
    }
}
