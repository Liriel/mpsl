// This file can be replaced during build by using the `fileReplacements` array.
// `ng build` replaces `environment.ts` with `environment.prod.ts`.
// The list of file replacements can be found in `angular.json`.

export const environment = {
  // empty = same-origin: every API/SignalR call goes to the Angular dev server and is forwarded to
  // the backend by ClientApp/proxy.conf.js, so dev matches production (no CORS, no cross-origin
  // credentials, relative links such as <a href="Account/SignIn"> resolve correctly).
  ServerUrl: "",
  production: false,
  vapidPublicKey: "BEa_VDNyon1zoaJ7333Cc-EcdEeRyva4rSgSR7OXycZNPOMR7vjBIa84FWEBSNSrMkRM_VbyPwmeUbHwp-i6QOY"
};

/*
 * For easier debugging in development mode, you can import the following file
 * to ignore zone related error stack frames such as `zone.run`, `zoneDelegate.invokeTask`.
 *
 * This import should be commented out in production mode because it will have a negative impact
 * on performance if an error is thrown.
 */
// import 'zone.js/plugins/zone-error';  // Included with Angular CLI.
