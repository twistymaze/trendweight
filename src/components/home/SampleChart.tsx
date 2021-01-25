import { Box, Center, Image } from "@chakra-ui/react";
import React from "react";

const SampleChart = () => (
  <Center gridArea="chart" pb={8}>
    <Box position="relative">
      <Image
        src="/assets/chart.png"
        height={{
          base: "auto",
          md: 220,
          lg: 285,
          xl: 370,
        }}
        width="auto"
        maxW="100%"
      />
      <Image
        src="/assets/withings-scale-white.png"
        height={{ base: 120, md: 180 }}
        width={{ base: 120, md: 180 }}
        maxW="25%"
        position="absolute"
        left={5}
        bottom={"-10%"}
      />
    </Box>
  </Center>
);

export default SampleChart;
