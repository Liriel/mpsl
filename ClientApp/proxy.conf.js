const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'http://localhost:41651';

const PROXY_CONFIG = [
  {
    context: [
      "/api",
      "/live"
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
