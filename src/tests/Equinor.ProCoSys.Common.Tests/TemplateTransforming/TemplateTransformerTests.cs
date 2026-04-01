using System.Dynamic;
using Equinor.ProCoSys.Common.TemplateTransforming;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Equinor.ProCoSys.Common.Tests.TemplateTransforming;

[TestClass]
public class TemplateTransformerTests
{
    private readonly TemplateTransformer _dut = new();

    // Tests where placeholders have the dollar notation {{$MyProp}} is just to be backward compatible with existing pcs4
    // email templates In pcs5 we don't support camelCased properties. However, we support camelCasing in placeholders to
    // be compatible with existing pcs4 email templates

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedSimpleProperties_ForCamelCasedPlacedHolders()
        => AssertTransformationOfSimpleProperties("P1: {{myStrProp}} P2: {{myIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedSimpleProperties_ForPascalCasedPlacedHolders()
        => AssertTransformationOfSimpleProperties("P1: {{MyStrProp}} P2: {{MyIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedSimpleProperties_ForPlacedHoldersWithDollar()
        => AssertTransformationOfSimpleProperties("P1: {{$MyStrProp}} P2: {{$myIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedClassProperties_ForCamelCasedPlacedHolders()
        => AssertTransformationOfClassProperties("P1: {{myStrProp}} P2: {{myIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedClassProperties_ForPascalCasedPlacedHolders()
        => AssertTransformationOfClassProperties("P1: {{MyStrProp}} P2: {{MyIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedClassProperties_ForPlacedHoldersWithDollar()
        => AssertTransformationOfClassProperties("P1: {{$MyStrProp}} P2: {{$myIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedDynamicProperties_ForCamelCasedPlacedHolders()
        => AssertTransformationOfDynamicProperties("P1: {{class1.myStrProp}} P2: {{class1.myIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedDynamicProperties_ForPascalCasedPlacedHolders()
        => AssertTransformationOfDynamicProperties("P1: {{Class1.MyStrProp}} P2: {{Class1.MyIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedDynamicProperties_ForPlacedHoldersWithDollar()
        => AssertTransformationOfDynamicProperties("P1: {{$Class1.MyStrProp}} P2: {{$class1.myIntProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedDynamicChildProperties_ForCamelCasedPlacedHolders()
        => AssertTransformationOfDynamicChildProperties(
            "P1: {{class1.MyStrProp}} P2: {{class1.MyIntProp}} P3: {{class1.otherClass.anotherStrProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedDynamicChildProperties_ForPascalCasedPlacedHolders()
        => AssertTransformationOfDynamicChildProperties(
            "P1: {{Class1.MyStrProp}} P2: {{Class1.MyIntProp}} P3: {{Class1.OtherClass.AnotherStrProp}}");

    [TestMethod]
    public void Transform_ShouldTransformPascalCasedDynamicChildProperties_ForPlacedHoldersWithDollar()
        => AssertTransformationOfDynamicChildProperties(
            "P1: {{$Class1.MyStrProp}} P2: {{$Class1.MyIntProp}} P3: {{$class1.OtherClass.anotherStrProp}}");

    [TestMethod]
    public void Transform_ShouldTransformCamelCasedProperties_WithUnknownPropertyValue()
    {
        // Arrange
        var template = "P1: {{myStrProp}}";
        var context = new { myStrProp = "Test" };

        // Act
        var result = _dut.Transform(template, context);

        // Assert
        Assert.AreEqual($"P1: {TemplateTransformer.UnknownPropertyValue}", result);
    }

    [TestMethod]
    public void Transform_ShouldTransformCamelCasedClassProperties_WithUnknownPropertyValue()
    {
        // Arrange
        var template = "P1: {{myStrProp}}";
        var context = new ClassWithCamelCasedProperty("Test");

        // Act
        var result = _dut.Transform(template, context);

        // Assert
        Assert.AreEqual($"P1: {TemplateTransformer.UnknownPropertyValue}", result);
    }

    private void AssertTransformationOfSimpleProperties(string template)
    {
        var context = new { MyStrProp = "Test", MyIntProp = 20 };

        // Act
        var result = _dut.Transform(template, context);

        // Assert
        Assert.AreEqual("P1: Test P2: 20", result);
    }

    private void AssertTransformationOfClassProperties(string template)
    {
        var context = new SomeClassWithPascalCasedProperties("Test", 20);

        // Act
        var result = _dut.Transform(template, context);

        // Assert
        Assert.AreEqual("P1: Test P2: 20", result);
    }

    private void AssertTransformationOfDynamicProperties(string template)
    {
        dynamic context = new ExpandoObject();
        context.Class1 = new SomeClassWithPascalCasedProperties("Test", 20);

        // Act
        var result = _dut.Transform(template, context);

        // Assert
        Assert.AreEqual("P1: Test P2: 20", result);
    }

    private void AssertTransformationOfDynamicChildProperties(string template)
    {
        dynamic context = new ExpandoObject();
        context.Class1 = new SomeClassWithPascalCasedProperties("Test", 20, new SomeOtherClass("Abc"));

        // Act
        var result = _dut.Transform(template, context);

        // Assert
        Assert.AreEqual("P1: Test P2: 20 P3: Abc", result);
    }
}

record SomeClassWithPascalCasedProperties(string MyStrProp, int MyIntProp, SomeOtherClass OtherClass = null);

record SomeOtherClass(string AnotherStrProp);

record ClassWithCamelCasedProperty(string myStrProp);
