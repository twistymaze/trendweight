import BaseDocument, { Head, Html, Main, NextScript } from "next/document";

class Document extends BaseDocument {
  render() {
    return (
      <Html>
        <Head>
          <link rel="preconnect" href="https://fonts.gstatic.com" />
          <link
            rel="preload"
            href="https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap"
            as="style"
          />
          <link href="https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap" rel="stylesheet" />
          <link
            rel="preload"
            href="https://fonts.googleapis.com/css2?family=Zilla+Slab:wght@700&display=swap"
            as="style"
          />
          <link
            href="https://fonts.googleapis.com/css2?family=Zilla+Slab:wght@700&display=swap"
            rel="stylesheet"
          ></link>
          <script async defer data-domain="trendweight.io" src="https://plausible.io/js/plausible.js"></script>
        </Head>
        <body>
          <Main />
          <NextScript />
        </body>
      </Html>
    );
  }
}

export default Document;
