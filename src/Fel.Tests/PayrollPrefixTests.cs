using Fel.Infrastructure.Services;

namespace Fel.Tests
{
    // El prefijo de una nómina sale de su numeración (dueña del consecutivo), no del valor por defecto del borrador.
    public class PayrollPrefixTests
    {
        [Fact]
        public void The_numbering_prefix_wins_over_the_default_of_the_draft()
        {
            // Caso real: borrador manual con el "NE" por defecto, numeración de nómina con prefijo "N" y "NE" ya usado como numeración de eliminación en Dataico.
            Assert.Equal("N", ResolutionNumbering.PayrollPrefix("NE", "N"));
        }

        [Fact]
        public void The_numbering_prefix_is_trimmed()
        {
            Assert.Equal("NOM", ResolutionNumbering.PayrollPrefix("NE", "  NOM "));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Without_a_numbering_prefix_the_draft_prefix_is_kept(string? numberingPrefix)
        {
            Assert.Equal("NE", ResolutionNumbering.PayrollPrefix("NE", numberingPrefix));
        }
    }
}
