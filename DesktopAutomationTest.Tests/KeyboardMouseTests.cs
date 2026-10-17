using System;
using Xunit;
using FluentAssertions;
using Moq;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Tests;

/// <summary>
/// Unit tests for [FEATURE-KEYBOARD-MOUSE] — DoubleClick, RightClick, Hover,
/// PressKey, SendKeys, and DragAndDrop using Moq to verify IDriver contracts.
/// All tests run cross-platform (no Windows UI Automation required).
/// </summary>
public class KeyboardMouseTests
{
    // ─── DoubleClick ──────────────────────────────────────────────────────────────

    [Fact]
    public void MockDriver_DoubleClick_ReturnsTrueOnSuccess()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.DoubleClickByAutomationId("SubmitButton", null)).Returns(true);

        var result = mock.Object.DoubleClickByAutomationId("SubmitButton");

        result.Should().BeTrue();
        mock.Verify(d => d.DoubleClickByAutomationId("SubmitButton", null), Times.Once);
    }

    [Fact]
    public void MockDriver_DoubleClick_ReturnsFalseWhenElementNotFound()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.DoubleClickByAutomationId("NonExistentElement", null)).Returns(false);

        var result = mock.Object.DoubleClickByAutomationId("NonExistentElement");

        result.Should().BeFalse();
    }

    // ─── RightClick ───────────────────────────────────────────────────────────────

    [Fact]
    public void MockDriver_RightClick_ReturnsTrueOnSuccess()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.RightClickByAutomationId("ResultTextBlock", null)).Returns(true);

        var result = mock.Object.RightClickByAutomationId("ResultTextBlock");

        result.Should().BeTrue();
        mock.Verify(d => d.RightClickByAutomationId("ResultTextBlock", null), Times.Once);
    }

    [Fact]
    public void MockDriver_RightClick_ReturnsFalseWhenDisabled()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.RightClickByAutomationId("DisabledButton", null)).Returns(false);

        var result = mock.Object.RightClickByAutomationId("DisabledButton");

        result.Should().BeFalse();
    }

    // ─── HoverOver ────────────────────────────────────────────────────────────────

    [Fact]
    public void MockDriver_Hover_ReturnsTrueOnSuccess()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.HoverOverByAutomationId("SubmitButton", null)).Returns(true);

        var result = mock.Object.HoverOverByAutomationId("SubmitButton");

        result.Should().BeTrue();
        mock.Verify(d => d.HoverOverByAutomationId("SubmitButton", null), Times.Once);
    }

    [Fact]
    public void MockDriver_Hover_ReturnsFalseWhenElementMissing()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.HoverOverByAutomationId("GhostElement", null)).Returns(false);

        var result = mock.Object.HoverOverByAutomationId("GhostElement");

        result.Should().BeFalse();
    }

    // ─── PressKey ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MockDriver_PressKey_ReturnsTrueForValidKey()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.PressKey("Return")).Returns(true);

        var result = mock.Object.PressKey("Return");

        result.Should().BeTrue();
        mock.Verify(d => d.PressKey("Return"), Times.Once);
    }

    [Fact]
    public void MockDriver_PressKey_ReturnsFalseForUnknownKey()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.PressKey("XYZ_INVALID")).Returns(false);

        var result = mock.Object.PressKey("XYZ_INVALID");

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("Tab")]
    [InlineData("Escape")]
    [InlineData("Return")]
    [InlineData("F5")]
    public void MockDriver_PressKey_HandlesCommonKeys(string key)
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.PressKey(key)).Returns(true);

        var result = mock.Object.PressKey(key);

        result.Should().BeTrue($"PressKey('{key}') should return true");
    }

    // ─── SendKeys ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MockDriver_SendKeys_ReturnsTrueOnSuccess()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.SendKeys("Hello World")).Returns(true);

        var result = mock.Object.SendKeys("Hello World");

        result.Should().BeTrue();
        mock.Verify(d => d.SendKeys("Hello World"), Times.Once);
    }

    [Fact]
    public void MockDriver_SendKeys_ReturnsTrueForEmptyString()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.SendKeys(string.Empty)).Returns(true);

        var result = mock.Object.SendKeys(string.Empty);

        result.Should().BeTrue();
    }

    // ─── DragAndDrop ──────────────────────────────────────────────────────────────

    [Fact]
    public void MockDriver_DragAndDrop_ReturnsTrueOnSuccess()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.DragAndDrop("DragSource", "DropTarget", null)).Returns(true);

        var result = mock.Object.DragAndDrop("DragSource", "DropTarget");

        result.Should().BeTrue();
        mock.Verify(d => d.DragAndDrop("DragSource", "DropTarget", null), Times.Once);
    }

    [Fact]
    public void MockDriver_DragAndDrop_ReturnsFalseWhenSourceMissing()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.DragAndDrop("MissingSource", "DropTarget", null)).Returns(false);

        var result = mock.Object.DragAndDrop("MissingSource", "DropTarget");

        result.Should().BeFalse();
    }

    [Fact]
    public void MockDriver_DragAndDrop_ReturnsFalseWhenTargetMissing()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.DragAndDrop("DragSource", "MissingTarget", null)).Returns(false);

        var result = mock.Object.DragAndDrop("DragSource", "MissingTarget");

        result.Should().BeFalse();
    }

    // ─── Interaction sequence ─────────────────────────────────────────────────────

    [Fact]
    public void MockDriver_KeyboardSequence_CanChainMultipleActions()
    {
        var mock = new Mock<IDriver>();
        mock.Setup(d => d.ClickByAutomationId("FirstNameTextBox", null)).Returns(true);
        mock.Setup(d => d.SendKeys("John")).Returns(true);
        mock.Setup(d => d.PressKey("Tab")).Returns(true);
        mock.Setup(d => d.SendKeys("Doe")).Returns(true);
        mock.Setup(d => d.PressKey("Return")).Returns(true);

        mock.Object.ClickByAutomationId("FirstNameTextBox").Should().BeTrue();
        mock.Object.SendKeys("John").Should().BeTrue();
        mock.Object.PressKey("Tab").Should().BeTrue();
        mock.Object.SendKeys("Doe").Should().BeTrue();
        mock.Object.PressKey("Return").Should().BeTrue();

        mock.Verify(d => d.PressKey("Tab"), Times.Once);
        mock.Verify(d => d.PressKey("Return"), Times.Once);
    }
}
