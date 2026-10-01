using SelectAI.Core.Enums;
using SelectAI.SmartActions;
using Xunit;

namespace SelectAI.Tests;

public class SmartContentDetectorTests
{
    [Theory]
    [InlineData("https://github.com/dotnet/wpf", "https://github.com/dotnet/wpf")]
    [InlineData("Visit www.google.com for info", "https://www.google.com")]
    [InlineData("Check out openai.com/blog now", "https://openai.com/blog")]
    public void DetectEntities_IdentifiesUrls(string text, string expectedPrefix)
    {
        var entities = SmartContentDetector.DetectEntities(text);
        var urlEntity = entities.FirstOrDefault(e => e.Type == ContentType.Url);

        Assert.NotNull(urlEntity);
        Assert.StartsWith(expectedPrefix, urlEntity.Value);
    }

    [Fact]
    public void DetectEntities_IdentifiesEmailAddresses()
    {
        var text = "Contact support at hello@example.com for queries";
        var entities = SmartContentDetector.DetectEntities(text);
        var emailEntity = entities.FirstOrDefault(e => e.Type == ContentType.Email);

        Assert.NotNull(emailEntity);
        Assert.Equal("hello@example.com", emailEntity.Value);
    }

    [Fact]
    public void DetectEntities_IdentifiesProgrammingCode()
    {
        var code = "public void Calculate(int x) { return x * 2; }";
        var entities = SmartContentDetector.DetectEntities(code);
        var codeEntity = entities.FirstOrDefault(e => e.Type == ContentType.Code);

        Assert.NotNull(codeEntity);
        Assert.Equal("Explain Code", codeEntity.DisplayLabel);
    }

    [Fact]
    public void DetectEntities_IdentifiesQuestionsAndSearchQueries()
    {
        var query = "How does TCP congestion control work?";
        var entities = SmartContentDetector.DetectEntities(query);
        var searchEntity = entities.FirstOrDefault(e => e.Type == ContentType.SearchQuery);

        Assert.NotNull(searchEntity);
        Assert.Equal("Search Query", searchEntity.DisplayLabel);
    }
}
