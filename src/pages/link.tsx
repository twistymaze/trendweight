import { Box, Button } from "@chakra-ui/react";
import { useState } from "react";
import { get } from "~/lib/api/fetch";
import { Page } from "~/lib/core/interfaces";

const LinkAccount: Page = () => {
  const [isLoading, setIsLoading] = useState(false);
  const onLinkWithings = async () => {
    setIsLoading(true);
    const { authorizationUrl } = await get<{ authorizationUrl: string }>("/api/withings/link");
    window.location.assign(authorizationUrl);
  };
  return (
    <Box>
      <Button onClick={onLinkWithings} isLoading={isLoading}>
        Link Withings Account
      </Button>
    </Box>
  );
};

LinkAccount.title = "Link Account";
LinkAccount.requireLogin = true;

export default LinkAccount;