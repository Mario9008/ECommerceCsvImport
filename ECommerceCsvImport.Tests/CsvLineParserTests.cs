using ECommerceCsvImport.Services;
using Xunit;

namespace ECommerceCsvImport.Tests;

public class CsvLineParserTests
{
    [Fact]
    public void Parse_SimpleLineWithoutQuotes()
    {
        var line = "1001,Ноутбук Lenovo IdeaPad 3,45990";

        var result = CsvLineParser.Parse(line);

        Assert.Equal(3, result.Count);
        Assert.Equal("1001", result[0]);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", result[1]);
    }

    [Fact]
    public void Parse_EmptyFields()
    {
        var line = "1011,Блок питания,,4990";

        var result = CsvLineParser.Parse(line);

        Assert.Equal(4, result.Count);
        Assert.Equal("", result[2]); 
    }

    [Fact]
    public void Parse_FieldWithCommaInsideQuotes()
    {
        var line = "1012,\"Ноутбук, игровой\",59990";

        var result = CsvLineParser.Parse(line);

        Assert.Equal(3, result.Count);
        Assert.Equal("Ноутбук, игровой", result[1]);
    }

    [Fact]
    public void Parse_EscapedQuotes()
    {
        var line = "1013,\"Название \"\"Special\"\"\",Описание";

        var result = CsvLineParser.Parse(line);

        Assert.Equal(3, result.Count);
        Assert.Equal("Название \"Special\"", result[1]);
    }
}