﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using IdentityServer4.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DistributedCacheStateDataFormatterExtensions
    {
        public static ISecureDataFormat<AuthenticationProperties> CreateDistributedCacheStateDataFormatter<TOptions>(this IServiceCollection services, string name)
            where TOptions : class
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));

            services.AddSingleton<IPostConfigureOptions<TOptions>, DistributedCacheStateDataFormatterInitializer<TOptions>>();
            return new DistributedCacheStateDataFormatterMarker(name);
        }
    }
}

namespace IdentityServer4.Hosting
{
    class DistributedCacheStateDataFormatterInitializer<TOptions> : IPostConfigureOptions<TOptions>
        where TOptions : class
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DistributedCacheStateDataFormatterInitializer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void PostConfigure(string name, TOptions options)
        {
            dynamic opts = options;
            if (opts.StateDataFormat is DistributedCacheStateDataFormatterMarker marker)
            {
                if (marker.Name == name)
                {
                    opts.StateDataFormat = new DistributedCacheStateDataFormatter(_httpContextAccessor, marker.Name);
                }
            }
        }
    }

    class DistributedCacheStateDataFormatterMarker : ISecureDataFormat<AuthenticationProperties>
    {
        public string Name { get; set; }

        public DistributedCacheStateDataFormatterMarker(string name)
        {
            Name = name;
        }

        public string Protect(AuthenticationProperties data)
        {
            throw new NotImplementedException();
        }

        public string Protect(AuthenticationProperties data, string purpose)
        {
            throw new NotImplementedException();
        }

        public AuthenticationProperties Unprotect(string protectedText)
        {
            throw new NotImplementedException();
        }

        public AuthenticationProperties Unprotect(string protectedText, string purpose)
        {
            throw new NotImplementedException();
        }
    }

    public class DistributedCacheStateDataFormatter : ISecureDataFormat<AuthenticationProperties>
    {
        private readonly IHttpContextAccessor _httpContext;
        private readonly string _name;

        public DistributedCacheStateDataFormatter(IHttpContextAccessor httpContext, string name)
        {
            _httpContext = httpContext;
            _name = name;
        }

        string CacheKeyPrefix => "DistributedCacheStateDataFormatter";

        IDistributedCache Cache => _httpContext.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
        IDataProtector Protector => _httpContext.HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(CacheKeyPrefix, _name);

        public string Protect(AuthenticationProperties data)
        {
            return Protect(data, null);
        }

        public string Protect(AuthenticationProperties data, string purpose)
        {
            var key = Guid.NewGuid().ToString();
            var cacheKey = $"{CacheKeyPrefix}-{purpose}-{key}";
            var json = ObjectSerializer.ToString(data);

            // Rather than encrypt the full AuthenticationProperties
            // cache the data and encrypt the key that points to the data
            Cache.SetString(cacheKey, json);

            return Protector.Protect(key);
        }

        public AuthenticationProperties Unprotect(string protectedText)
        {
            return Unprotect(protectedText, null);
        }

        public AuthenticationProperties Unprotect(string protectedText, string purpose)
        {
            // Decrypt the key and retrieve the data from the cache.
            var key = Protector.Unprotect(protectedText);
            var cacheKey = $"{CacheKeyPrefix}-{purpose}-{key}";
            var json = Cache.GetString(cacheKey);

            return ObjectSerializer.FromString<AuthenticationProperties>(json);
        }
    }
}
