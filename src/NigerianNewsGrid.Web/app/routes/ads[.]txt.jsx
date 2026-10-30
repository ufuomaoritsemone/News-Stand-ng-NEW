import { textResponse } from "../utils/xml";

/**
 * Google AdSense Authorized Digital Sellers (ads.txt) verification endpoint.
 * Serves authorized seller identification with strict text/plain headers and caching.
 */
export async function loader() {
  const adsTxt = "google.com, pub-0810356418854639, DIRECT, f08c47fec0942fa0\n";
  return textResponse(adsTxt, 86400); // Cache verification record for 24h
}

