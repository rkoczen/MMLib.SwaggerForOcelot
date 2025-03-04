﻿using Kros.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MMLib.SwaggerForOcelot.Configuration;
using MMLib.SwaggerForOcelot.Transformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MMLib.SwaggerForOcelot.Middleware
{
    /// <summary>
    /// Swagger for Ocelot middleware.
    /// This middleware generate swagger documentation from downstream services for SwaggerUI.
    /// </summary>
    public class SwaggerForOcelotMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly IOptions<List<ReRouteOptions>> _reRoutes;
        private readonly Lazy<Dictionary<string, SwaggerEndPointOptions>> _swaggerEndPoints;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISwaggerJsonTransformer _transformer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SwaggerForOcelotMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next delegate.</param>
        /// <param name="options">The options.</param>
        /// <param name="reRoutes">The Ocelot ReRoutes configuration.</param>
        /// <param name="swaggerEndPoints">The swagger end points.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public SwaggerForOcelotMiddleware(
            RequestDelegate next,
            SwaggerForOcelotUIOptions options,
            IOptions<List<ReRouteOptions>> reRoutes,
            IOptions<List<SwaggerEndPointOptions>> swaggerEndPoints,
            IHttpClientFactory httpClientFactory,
            ISwaggerJsonTransformer transformer)
        {
            _transformer = Check.NotNull(transformer, nameof(transformer));
            _next = Check.NotNull(next, nameof(next));
            _reRoutes = Check.NotNull(reRoutes, nameof(reRoutes));
            Check.NotNull(swaggerEndPoints, nameof(swaggerEndPoints));
            _httpClientFactory = Check.NotNull(httpClientFactory, nameof(httpClientFactory));

            _swaggerEndPoints = new Lazy<Dictionary<string, SwaggerEndPointOptions>>(()
                => swaggerEndPoints.Value.ToDictionary(p => $"/{p.KeyToPath}", p => p));
        }

        /// <summary>
        /// Invokes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        public async Task Invoke(HttpContext context)
        {
            var endPoint = GetEndPoint(context.Request.Path);
            var httpClient = _httpClientFactory.CreateClient();

            var content = await httpClient.GetStringAsync(endPoint.Url);
            var hostName = endPoint.EndPoint.HostOverride ?? context.Request.Host.Value;
            content = _transformer.Transform(content, _reRoutes.Value.Where(p => p.SwaggerKey == endPoint.EndPoint.Key), hostName);

            await context.Response.WriteAsync(content);
        }

        /// <summary>
        /// Get Url and Endpoint from path
        /// </summary>
        /// <param name="path"></param>
        /// <returns>
        /// The Url of a specific version and <see cref="SwaggerEndPointOptions"/>.
        /// </returns>
        private (string Url, SwaggerEndPointOptions EndPoint) GetEndPoint(string path)
        {
            var endPointInfo = GetEndPointInfo(path);
            var endPoint = _swaggerEndPoints.Value[$"/{endPointInfo.Key}"];
            var url = endPoint.Config.FirstOrDefault(x => x.Version == endPointInfo.Version)?.Url;
            return (url, endPoint);
        }

        /// <summary>
        /// Get url and version from Path
        /// </summary>
        /// <param name="path"></param>
        /// <returns>
        /// Version and the key of End point
        /// </returns>
        private (string Version, string Key) GetEndPointInfo(string path)
        {
            var keys = path.Split('/');
            return (keys[1], keys[2]);
        }
    }
}
