import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "VocXuan · 我的单词本",
  description: "我的背词、到期复习与每日记录，手机电脑同步。",
  icons: {
    icon: "/favicon.svg",
    shortcut: "/favicon.svg",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="zh-CN">
      <body className="antialiased">{children}</body>
    </html>
  );
}
