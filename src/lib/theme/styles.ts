const styles = {
  global: () => ({
    html: {
      minWidth: "360px",
      overflowY: "scroll",
      overflowX: "hidden",
    },
    "#__next": {
      display: "flex",
      flexDirection: "column",
      minHeight: "100vh",
      background: "white",
    },
    "*": {
      WebkitTapHighlightColor: "transparent",
    },
  }),
};

export default styles;
