module.exports = {
  flowFile: "flows.json",
  uiPort: process.env.PORT || 1880,
  credentialSecret: false,
  functionExternalModules: false,
  contextStorage: {
    default: {
      module: "localfilesystem",
      config: {
        dir: "/data/.context"
      }
    }
  },
  logging: {
    console: {
      level: "info",
      metrics: false,
      audit: false
    }
  },
  editorTheme: {
    projects: {
      enabled: false
    }
  }
};
