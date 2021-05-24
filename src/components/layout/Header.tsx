import { Box } from "@chakra-ui/react";
import NextLink from "next/link";
import React, { useCallback } from "react";
import Logo from "~/components/shared/Logo";
import { useAuth } from "~/lib/core/auth";
import LayoutContainer from "./LayoutContainer";
import Navbar from "./nav/Navbar";
import NavLink from "./nav/NavLink";

const Header = () => {
  const auth = useAuth();
  const handleSignOut = useCallback(() => {
    auth.signOut();
  }, [auth]);

  const authLoaded = !auth.isInitializing;
  const isLoggedIn = !!auth?.user;

  return (
    <Box as="header" color="white" bg="brand.500">
      <LayoutContainer as="nav">
        <Navbar>
          <Navbar.Brand>
            <NextLink href="/">
              <a>
                <Box fontFamily="Zilla Slab" fontSize="2rem" fontWeight="bold" lineHeight="1.25rem">
                  TrendWeight
                </Box>
              </a>
            </NextLink>
            <Logo height="32px" width="77.13px" />
          </Navbar.Brand>
          <Navbar.Links>
            {authLoaded && (
              <>
                <NavLink href="/">Home</NavLink>
                <NavLink href="/dashboard" show={isLoggedIn}>
                  Dashboard
                </NavLink>
                <NavLink href="/settings" show={isLoggedIn}>
                  Settings
                </NavLink>
                <NavLink href="/about">Learn</NavLink>
                <NavLink href="/signup" show={!isLoggedIn}>
                  Sign Up
                </NavLink>
                <NavLink href="/login" show={!isLoggedIn}>
                  Log In
                </NavLink>
                <NavLink onClick={handleSignOut} show={isLoggedIn}>
                  Log Out
                </NavLink>
              </>
            )}
          </Navbar.Links>
        </Navbar>
      </LayoutContainer>
    </Box>
  );
};

// const Header = () => {
//   return (
//     <Box as="header" color="white" bg="brand.500">
//       <LayoutContainer as="nav" display="flex" flexDir="row" alignItems="center" justifyContent="space-between" px={4}>
//         <Flex direction="row" wrap="wrap" align="center" justify="space-between" w="full">
//           <Stack direction="row" spacing={2} py={3}>
//             <NextLink href="/">
//               <a>
//                 <Box fontFamily="Zilla Slab" fontSize="2rem" fontWeight="bold" lineHeight={1.25}>
//                   TrendWeight
//                 </Box>
//               </a>
//             </NextLink>
//             <Logo height="32px" width="77.13px" />
//           </Stack>
//           <Menu />
//         </Flex>
//       </LayoutContainer>
//     </Box>
//   );
// };

export default Header;
