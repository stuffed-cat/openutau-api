using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class ProjectExpressionsControllerTests
    {
        private readonly ProjectExpressionsController _controller;

        public ProjectExpressionsControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new ProjectExpressionsController();
        }

        [Fact]
        public void GetExpressions_ReturnsExpressions()
        {
            SetupHelper.CreateAndLoadRealProject(project => {
                project.expressions["vel"] = new UExpressionDescriptor("vel", "v", 0, 100, 50);
            });

            var result = _controller.GetExpressions();
            var okResult = result as OkObjectResult;

            Assert.NotNull(okResult);
            Assert.Equal(200, okResult.StatusCode);
            var expressions = okResult.Value as System.Collections.IEnumerable;
            Assert.NotNull(expressions);
            var exprList = expressions.Cast<dynamic>(); Assert.Contains(exprList, e => ((string)e.GetType().GetProperty("abbr").GetValue(e, null)) == "v");
        }
    }
}