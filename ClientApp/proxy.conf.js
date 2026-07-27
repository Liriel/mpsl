const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:5001';

const PROXY_CONFIG = [
  {
    context: [
      "/api",
      "/live",
      // AccountController is routed as [Route("[controller]")], so it has no /api prefix - without
      // this the relative <a href="Account/SignIn"> in login.component.html hits the Angular dev
      // server, falls back to index.html and dies as a router "cannot match any routes" error.
      "/Account",
      // the browser-facing OAuth callback of the MicrosoftAccount handler
      "/signin-microsoft"
   ],
    target: target,
    secure: false,
    headers: {
      Connection: 'Keep-Alive'
    },
    ws: true,
    changeOrigin: true
  }
]

module.exports = PROXY_CONFIG;
