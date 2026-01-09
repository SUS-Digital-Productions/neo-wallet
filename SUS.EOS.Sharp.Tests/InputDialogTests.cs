using System.Threading.Tasks;
using SUS.EOS.NeoWallet.Pages.Components;
using Xunit;

namespace SUS.EOS.Sharp.Tests
{
    public class InputDialogTests
    {
        [Fact]
        public async Task SimulateAccept_ReturnsProvidedText()
        {
            var dlg = new InputDialog("Title", "Message", "OK", "Cancel", isPassword: true);
            var result = await dlg.SimulateAcceptAsync("supersecret");
            Assert.Equal("supersecret", result);
        }

        [Fact]
        public async Task SimulateCancel_ReturnsNull()
        {
            var dlg = new InputDialog("Title", "Message", "OK", "Cancel", isPassword: true);
            var result = await dlg.SimulateCancelAsync();
            Assert.Null(result);
        }

        [Fact]
        public void EntryKeyboard_Property_Roundtrips()
        {
            var dlg = new InputDialog("Title", "Message");
            // Default keyboard should be available
            Assert.NotNull(dlg.EntryKeyboard);
            dlg.EntryKeyboard = Keyboard.Url;
            Assert.Equal(Keyboard.Url, dlg.EntryKeyboard);
        }

        [Fact]
        public async Task ShowAsync_Uses_ShowAsyncHandler()
        {
            var original = InputDialog.ShowAsyncHandler;
            try
            {
                InputDialog? captured = null;
                object? passedParent = null;

                InputDialog.ShowAsyncHandler = (dlg, parent) =>
                {
                    captured = dlg;
                    passedParent = parent;
                    return Task.FromResult<string?>("handler-result");
                };

                var dlg = new InputDialog("T", "M");
                var res = await dlg.ShowAsync(parent: null);

                Assert.Equal("handler-result", res);
                Assert.Same(dlg, captured);
                Assert.Null(passedParent);
            }
            finally
            {
                InputDialog.ShowAsyncHandler = original;
            }
        }
    }
}