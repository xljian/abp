﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Features;
using Volo.Abp.Localization;
using Volo.Abp.Settings;
using Volo.Abp.Users;

namespace Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations
{
    public class AbpApplicationConfigurationAppService : ApplicationService, IAbpApplicationConfigurationAppService
    {
        private readonly AbpLocalizationOptions _localizationOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAbpAuthorizationPolicyProvider _abpAuthorizationPolicyProvider;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUser _currentUser;
        private readonly ISettingProvider _settingProvider;
        private readonly ISettingDefinitionManager _settingDefinitionManager;
        private readonly IFeatureDefinitionManager _featureDefinitionManager;

        public AbpApplicationConfigurationAppService(
            IOptions<AbpLocalizationOptions> localizationOptions,
            IServiceProvider serviceProvider,
            IAbpAuthorizationPolicyProvider abpAuthorizationPolicyProvider,
            IAuthorizationService authorizationService,
            ICurrentUser currentUser, 
            ISettingProvider settingProvider, 
            SettingDefinitionManager settingDefinitionManager, 
            IFeatureDefinitionManager featureDefinitionManager)
        {
            _serviceProvider = serviceProvider;
            _abpAuthorizationPolicyProvider = abpAuthorizationPolicyProvider;
            _authorizationService = authorizationService;
            _currentUser = currentUser;
            _settingProvider = settingProvider;
            _settingDefinitionManager = settingDefinitionManager;
            _featureDefinitionManager = featureDefinitionManager;
            _localizationOptions = localizationOptions.Value;
        }

        public virtual async Task<ApplicationConfigurationDto> GetAsync()
        {
            //TODO: Optimize & cache..?

            return new ApplicationConfigurationDto
            {
                Auth = await GetAuthConfigAsync(),
                Features = await GetFeaturesConfigAsync(),
                Localization = GetLocalizationConfig(),
                CurrentUser = GetCurrentUser(),
                Setting = await GetSettingConfigAsync()
            };
        }

        protected virtual CurrentUserDto GetCurrentUser()
        {
            return new CurrentUserDto
            {
                IsAuthenticated = _currentUser.IsAuthenticated,
                Id = _currentUser.Id,
                TenantId = _currentUser.TenantId,
                UserName = _currentUser.UserName
            };
        }

        protected virtual async Task<ApplicationAuthConfigurationDto> GetAuthConfigAsync()
        {
            var authConfig = new ApplicationAuthConfigurationDto();

            foreach (var policyName in await _abpAuthorizationPolicyProvider.GetPoliciesNamesAsync())
            {
                authConfig.Policies[policyName] = true;

                if (await _authorizationService.IsGrantedAsync(policyName))
                {
                    authConfig.GrantedPolicies[policyName] = true;
                }
            }

            return authConfig;
        }

        protected virtual ApplicationLocalizationConfigurationDto GetLocalizationConfig()
        {
            var localizationConfig = new ApplicationLocalizationConfigurationDto();

            foreach (var resource in _localizationOptions.Resources.Values)
            {
                var dictionary = new Dictionary<string, string>();

                var localizer = _serviceProvider.GetRequiredService(
                    typeof(IStringLocalizer<>).MakeGenericType(resource.ResourceType)
                ) as IStringLocalizer;

                foreach (var localizedString in localizer.GetAllStrings())
                {
                    dictionary[localizedString.Name] = localizedString.Value;
                }

                var resourceName = LocalizationResourceNameAttribute.GetName(resource.ResourceType);
                localizationConfig.Values[resourceName] = dictionary;
            }

            return localizationConfig;
        }

        private async Task<ApplicationSettingConfigurationDto> GetSettingConfigAsync()
        {
            var result = new ApplicationSettingConfigurationDto
            {
                Values = new Dictionary<string, string>()
            };

            foreach (var settingDefinition in _settingDefinitionManager.GetAll())
            {
                if (!settingDefinition.IsVisibleToClients)
                {
                    continue;
                }

                result.Values[settingDefinition.Name] = await _settingProvider.GetOrNullAsync(settingDefinition.Name);
            }

            return result;
        }

        protected virtual async Task<ApplicationFeatureConfigurationDto> GetFeaturesConfigAsync()
        {
            var result = new ApplicationFeatureConfigurationDto();

            foreach (var featureDefinition in _featureDefinitionManager.GetAll())
            {
                if (!featureDefinition.IsVisibleToClients)
                {
                    continue;
                }

                result.Values[featureDefinition.Name] = await FeatureChecker.GetOrNullAsync(featureDefinition.Name);
            }

            return result;
        }
    }
}