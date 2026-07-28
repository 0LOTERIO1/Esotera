import type { NextConfig } from "next";

const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080";
let apiHostname = "localhost";
let apiPort = "5080";
let apiProtocol: "http" | "https" = "http";
try {
  const parsed = new URL(apiUrl);
  apiHostname = parsed.hostname;
  apiPort = parsed.port || (parsed.protocol === "https:" ? "443" : "80");
  apiProtocol = parsed.protocol === "https:" ? "https" : "http";
} catch {
  // mantém defaults
}

const nextConfig: NextConfig = {
  images: {
    dangerouslyAllowSVG: true,
    contentSecurityPolicy: "default-src 'self'; script-src 'none'; sandbox;",
    remotePatterns: [
      {
        protocol: apiProtocol,
        hostname: apiHostname,
        port: apiPort,
        pathname: "/media/**",
      },
      {
        protocol: "https",
        hostname: "res.cloudinary.com",
        pathname: "/y3gghtzw/image/upload/**",
      },
    ],
  },
};

export default nextConfig;
