﻿namespace formulate.app.Composers
{
    // Namespaces.
    using System.Configuration;
    using System.Xml;

    using formulate.app.Backoffice;
    using formulate.app.Backoffice.Dashboards;
    using formulate.app.Components;
    using formulate.app.Configuration;

    using Umbraco.Core;
    using Umbraco.Core.Composing;
    using Umbraco.Core.Logging;
    using Umbraco.Web;

    using MetaConstants = meta.Constants;
    using Resources = Properties.Resources;
    using SettingConstants = core.Constants.Settings;

    /// <summary>
    /// Handles the application started event.
    /// </summary>
    public class ApplicationStartedUserComposer : IUserComposer
    {
        #region Constants

        private const string DeveloperSectionXPath = @"/dashBoard/section[@alias='StartupDeveloperDashboardSection']";
        private const string MissingDeveloperSection = @"Unable to locate StartupDeveloperDashboardSection in the dashboard.config. The Formulate tab will not be added to the Developer section.";
        private const string InstallActionsError = @"An unknown error occurred while attempting to asynchronously run the install actions for Formulate.";
        private const string TableCreationError = @"An error occurred while attempting to create the FormulateSubmissions table.";

        #endregion


        #region Properties

        private ILogger Logger { get; set; }
        #endregion


        #region Constructors

        public ApplicationStartedUserComposer(ILogger logger)
        {
            Logger = logger;
        }

        #endregion


        #region Methods
        public void Compose(Composition composition)
        {
            InitializeConfiguration(composition);
            HandleInstallAndUpgrade(composition);
            InitializeDatabase(composition);
            InitializeServerVariables(composition);
        }

        private void InitializeConfiguration(Composition composition)
        {
            composition.Configs.Add<IPersistenceConfig>("formulateConfiguration/persistence");
            composition.Configs.Add<ITemplatesConfig>("formulateConfiguration/templates");
        }

        private void InitializeServerVariables(Composition composition)
        {
            composition.Components().Append<ServerVariablesComponent>();
        }

        /// <summary>
        /// Modifies the database (e.g., adding necessary tables).
        /// </summary>
        /// <param name="applicationContext">
        /// The application context.
        /// </param>
        private void InitializeDatabase(Composition composition)
        {
            composition.Components().Append<InstallDatabaseMigrationComponent>();
        }

        /// <summary>
        /// Handles install and upgrade operations.
        /// </summary>
        private void HandleInstallAndUpgrade(
            Composition composition)
        {
            var version = GetInstalledVersion();
            var isInstalled = version != null;
            var needsUpgrade = !MetaConstants.Version.InvariantEquals(version);
            if (!isInstalled)
            {

                // Logging.
                Logger.Info<ApplicationStartedUserComposer>("Installing Formulate.");

                // Install Formulate.
                HandleInstall(composition);

            }
            else if (needsUpgrade)
            {
                // Logging.
                Logger.Info<ApplicationStartedUserComposer>("Upgrading Formulate.");

                // Perform an upgrade installation.
                HandleInstall(composition, true);
            }
        }


        /// <summary>
        /// Handles install operations.
        /// </summary>
        /// <param name="isUpgrade">
        /// Is this an upgrade to an existing instllation?
        /// </param>
        /// <param name="applicationContext">
        /// The current Umbraco application context.
        /// </param>
        private void HandleInstall(Composition composition, bool isUpgrade = false)
        {

            // Add the Formulate section and the Formulate dashboard in the Formulate section.
            AddSection(composition);
            AddFormulateDashboard(composition);


            // If this is a new install, add the Formulate dashboard in the Developer section,
            // and check if users need to be given access to Formulate.
            if (!isUpgrade)
            {
                this.AddFormulateDeveloperDashboard(composition);
                PermitAccess();
            }


            // Make changes to the web.config.
            AddConfigurationGroup();
            EnsureAppSettings();

        }


        /// <summary>
        /// Gets the installed version.
        /// </summary>
        /// <returns>The installed version, or null.</returns>
        private string GetInstalledVersion()
        {
            var key = SettingConstants.VersionKey;
            var version = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(version))
            {
                version = null;
            }
            return version;
        }


        /// <summary>
        /// Indicates whether or not the application setting with the specified key has a non-empty
        /// value in the web.config.
        /// </summary>
        /// <param name="key">
        /// The application setting key.
        /// </param>
        /// <returns>
        /// True, if the value in the web.config is non-empty; otherwise, false.
        /// </returns>
        private bool DoesAppSettingExist(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            return !string.IsNullOrWhiteSpace(value);
        }


        /// <summary>
        /// Adds the Formulate section to Umbraco.
        /// </summary>
        /// <param name="applicationContext">
        /// The current application context.
        /// </param>
        private void AddSection(Composition composition)
        {
            composition.Sections().Append<FormulateSection>();
        }


        /// <summary>
        /// Adds the Formulate dashboard to the Formulate section.
        /// </summary>
        private void AddFormulateDashboard(Composition composition)
        {
            composition.Dashboards().Add<FormulateDashboard>();
            //// Queue dashboard transformation.
            //QueueInstallAction(() =>
            //{
            //    var exists = DashboardExists();
            //    if (!exists)
            //    {

            //        // Logging.
            //        Logger.Info<ApplicationStartedUserComposer>("Installing Formulate dashboard.");


            //        // Variables.
            //        var doc = new XmlDocument();
            //        var actionXml = Resources.TransformDashboard;
            //        doc.LoadXml(actionXml);

            //        // Add dashboard.
            //        PackageAction.RunPackageAction("Formulate",
            //            "Formulate.TransformXmlFile", doc.FirstChild);

            //        // Logging.
            //        Logger.Info<ApplicationStartedUserComposer>("Done installing Formulate dashboard.");

            //    }
            //});

        }

        /// <summary>
        /// Adds the "Formulate" tab to the developer section of the
        /// dashboard.config.
        /// </summary>
        private void AddFormulateDeveloperDashboard(Composition composition)
        {
            composition.Dashboards().Add<FormulateDeveloperDashboard>();
        }


        /// <summary>
        /// Adds or replaces the Formulate version number in the web.config, along with some other
        /// application settings.
        /// </summary>
        private void EnsureAppSettings()
        {

            // Queue the web.config change.
            //QueueInstallAction(() =>
            //{

            //    // Logging.
            //    Logger.Info<ApplicationStartedUserComposer>("Ensuring Formulate version in the web.config.");


            //    // Variables.
            //    var key = SettingConstants.VersionKey;
            //    var config = WebConfigurationManager.OpenWebConfiguration("~");
            //    var settings = config.AppSettings.Settings;
            //    var formulateKeys = new[]
            //    {
            //        new
            //        {
            //            key = SettingConstants.RecaptchaSiteKey,
            //            value = string.Empty
            //        },

            //        new {
            //          key = SettingConstants.RecaptchaSecretKey,
            //          value = string.Empty
            //        },

            //        new {
            //            key = SettingConstants.EnableJSONFormLogging,
            //            value = "false"
            //        }
            //    };



            //    // Replace the version setting.
            //    if (settings.AllKeys.Any(x => key.InvariantEquals(x)))
            //    {
            //        settings.Remove(key);
            //    }
            //    settings.Add(key, MetaConstants.Version);


            //    // Ensure the Recaptcha keys exist in the web.config.
            //    foreach (var configKey in formulateKeys)
            //    {
            //        if (!DoesAppSettingExist(configKey.key))
            //        {
            //            settings.Add(configKey.key, configKey.value);
            //        }
            //    }


            //    // Save config changes.
            //    config.Save();


            //    // Logging.
            //    Logger.Info<ApplicationStartedUserComposer>("Done ensuring Formulate version in the web.config.");

            //});

        }


        /// <summary>
        /// Permits all users to access Formulate if configured in the web.config.
        /// </summary>
        private void PermitAccess()
        {

            // Variables.
            var key = SettingConstants.EnsureUsersCanAccess;
            var ensure = ConfigurationManager.AppSettings[key];


            // Should all users be given access to Formulate?
            if (string.IsNullOrWhiteSpace(ensure))
            {
                return;
            }


            // Variables.
            var doc = new XmlDocument();
            var actionXml = Resources.GrantAllUsersPermissionToSection;
            doc.LoadXml(actionXml);


            // Grant access permission.

            //TODO: Reinstate
            //PackageAction.RunPackageAction("Formulate",
            //    "Formulate.GrantPermissionToSection", doc.FirstChild);

        }


        /// <summary>
        /// Transforms the web.config to add the Formulate configuration group.
        /// </summary>
        private void AddConfigurationGroup()
        {

            // Queue web.config change to add Formulate configuration.
            //QueueInstallAction(() =>
            //{

            //    // Does the section group already exist and contain all the expected sections?
            //    var config = WebConfigurationManager.OpenWebConfiguration("~");
            //    var groupName = "formulateConfiguration";
            //    var group = config.GetSectionGroup(groupName);
            //    var exists = group != null;
            //    var sectionKeys = (group?.Sections?.Keys?.Cast<string>()?.ToArray()).MakeSafe();
            //    var sectionsSet = new HashSet<string>(sectionKeys);
            //    var expectedSections = new[]
            //    {
            //        "buttons",
            //        "emailWhitelist",
            //        "email",
            //        "fieldCategories",
            //        "persistence",
            //        "submissions",
            //        "templates"
            //    };
            //    var containsAllSections = expectedSections.All(x => sectionsSet.Contains(x));


            //    // Only add the group if it doesn't exist or doesn't contain all the expected sections.
            //    if (!exists || !containsAllSections)
            //    {

            //        // Logging.
            //        Logger.Info<ApplicationStartedUserComposer>("Adding Formulate config to the web.config.");


            //        // Variables.
            //        var doc = new XmlDocument();
            //        var actionXml = Resources.TransformWebConfig;
            //        doc.LoadXml(actionXml);


            //        // Add configuration group.
            //        PackageAction.RunPackageAction("Formulate",
            //            "Formulate.TransformXmlFile", doc.FirstChild);


            //        // Logging.
            //        Logger.Info<ApplicationStartedUserComposer>("Done adding Formulate config to the web.config.");

            //    }

            //});

        }

        #endregion
    }
}